using System.Collections.Immutable;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class WeeklyHoursTests
{
    public static WorkingDayWrite[] Days(int day, params OpeningPeriodWrite[] periods) => Enumerable.Range(0, 7)
        .Select(index => new WorkingDayWrite(index, index != day, index == day ? periods : [])).ToArray();
    public static WeeklyHours Hours(int day, params OpeningPeriodWrite[] periods)
    {
        Assert.True(HoursInput.Normalize(Days(day, periods), out var hours)); return hours!;
    }
    [Fact]
    public void All_days_must_be_explicit_unique_and_valid_with_strict_minute_times()
    {
        Assert.False(HoursInput.Normalize(null, out _)); Assert.False(HoursInput.Normalize([], out _));
        Assert.False(HoursInput.Normalize(Days(1, new OpeningPeriodWrite("09:00", "17:00")).Skip(1).ToArray(), out _));
        var duplicate = Days(1, new OpeningPeriodWrite("09:00", "17:00")); duplicate[0] = duplicate[1]; Assert.False(HoursInput.Normalize(duplicate, out _));
        var unknown = Days(1, new OpeningPeriodWrite("09:00", "17:00")); unknown[0] = new(7, true, []); Assert.False(HoursInput.Normalize(unknown, out _));
        Assert.False(WeeklyHours.TryCreate([new(0, true, default), .. WeeklyHours.AllClosed.Days.Skip(1)], out _));
        Assert.False(HoursInput.Normalize([null!, .. Days(1, new OpeningPeriodWrite("09:00", "17:00")).Skip(1)], out _));
        var missing = Days(1, new OpeningPeriodWrite("09:00", "17:00")); missing[1] = missing[1] with { Periods = null! }; Assert.False(HoursInput.Normalize(missing, out _));
    }
    [Theory]
    [InlineData("9:00", "17:00", false)]
    [InlineData("09:00:00", "17:00", false)]
    [InlineData("09:00", "24:00", false)]
    [InlineData("09:00", "09:00", false)]
    [InlineData("17:00", "09:00", false)]
    [InlineData("09:00", "17:00", true)]
    [InlineData(" 09:00", "17:00", false)]
    [InlineData(null, "17:00", false)]
    public void Invalid_or_longer_than_one_day_periods_are_rejected(string? opens, string closes, bool nextDay) =>
        Assert.False(HoursInput.Normalize(Days(1, new OpeningPeriodWrite(opens!, closes, nextDay)), out _));
    [Fact]
    public void Closed_flags_period_counts_precision_and_overlaps_are_enforced()
    {
        var closed = Days(1, new OpeningPeriodWrite("09:00", "17:00")); closed[1] = closed[1] with { Closed = true }; Assert.False(HoursInput.Normalize(closed, out _));
        Assert.False(HoursInput.Normalize(Days(1), out _));
        Assert.False(HoursInput.Normalize(Days(1, Enumerable.Repeat(new OpeningPeriodWrite("09:00", "10:00"), 9).ToArray()), out _));
        Assert.False(HoursInput.Normalize(Days(1, new OpeningPeriodWrite("09:00", "12:00"), new("11:59", "13:00")), out _));
        var domain = WeeklyHours.AllClosed.Days.ToArray(); domain[1] = new(1, false, [new(new(9, 0, 1), new(10, 0), false)]); Assert.False(WeeklyHours.TryCreate(domain, out _));
    }
    [Fact]
    public void Overnight_overlap_including_the_week_boundary_is_rejected_but_adjacency_is_valid()
    {
        var days = Days(6, new OpeningPeriodWrite("22:00", "02:00", true)); days[0] = new(0, false, [new("01:59", "03:00")]); Assert.False(HoursInput.Normalize(days, out _));
        days[0] = new(0, false, [new("02:00", "03:00")]); Assert.True(HoursInput.Normalize(days, out var hours));
        var state = hours!.At(DateTimeOffset.Parse("2026-11-28T23:59:59Z")); Assert.True(state.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-11-29T03:00:00+02:00"), state.ClosesAt);
    }
    [Theory]
    [InlineData("2026-11-23T06:59:59Z", false, "2026-11-23T09:00:00+02:00", null)]
    [InlineData("2026-11-23T07:00:00Z", true, null, "2026-11-23T12:00:00+02:00")]
    [InlineData("2026-11-23T09:59:59Z", true, null, "2026-11-23T12:00:00+02:00")]
    [InlineData("2026-11-23T10:00:00Z", false, "2026-11-23T13:00:00+02:00", null)]
    [InlineData("2026-11-23T15:00:00Z", false, "2026-11-30T09:00:00+02:00", null)]
    public void Opening_is_inclusive_closing_exclusive_and_next_open_crosses_closed_days(string instant, bool open, string? next, string? close)
    {
        var state = Hours(1, new OpeningPeriodWrite("09:00", "12:00"), new("13:00", "17:00")).At(DateTimeOffset.Parse(instant));
        Assert.Equal(open, state.OpenNow); Assert.Equal(next is null ? null : DateTimeOffset.Parse(next), state.NextOpen); Assert.Equal(close is null ? null : DateTimeOffset.Parse(close), state.ClosesAt);
        Assert.Equal("Africa/Cairo", state.TimeZone);
    }
    [Fact]
    public void Overnight_hours_continue_on_a_day_with_no_new_openings()
    {
        var hours = Hours(6, new OpeningPeriodWrite("22:00", "02:00", true));
        Assert.True(hours.At(DateTimeOffset.Parse("2026-11-28T23:00:00Z")).OpenNow);
        var closed = hours.At(DateTimeOffset.Parse("2026-11-29T00:00:00Z")); Assert.False(closed.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-12-05T22:00:00+02:00"), closed.NextOpen);
    }
    [Fact]
    public void Always_closed_and_continuously_open_have_no_invented_next_transition()
    {
        var instant = DateTimeOffset.Parse("2026-11-26T10:00:00Z"); var closed = WeeklyHours.AllClosed.At(instant);
        Assert.False(closed.OpenNow); Assert.Null(closed.NextOpen); Assert.Null(closed.ClosesAt);
        var days = Enumerable.Range(0, 7).Select(day => new WorkingDayWrite(day, false, [new("00:00", "00:00", true)])).ToArray();
        Assert.True(HoursInput.Normalize(days, out var hours)); var open = hours!.At(instant); Assert.True(open.OpenNow); Assert.Null(open.NextOpen); Assert.Null(open.ClosesAt);
    }
    [Fact]
    public void Adjacent_periods_are_one_continuous_opening_and_data_remains_immutable_and_canonical()
    {
        var input = Days(1, new OpeningPeriodWrite("12:00", "17:00"), new("09:00", "12:00")); Assert.True(HoursInput.Normalize(input, out var hours));
        input[1] = new(1, true, []); var state = hours!.At(DateTimeOffset.Parse("2026-11-23T09:59:59Z"));
        Assert.True(state.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-11-23T17:00:00+02:00"), state.ClosesAt);
        var json = WeeklyHoursJson.Write(hours); Assert.Equal(json, WeeklyHoursJson.Write(WeeklyHoursJson.Read(json))); Assert.Equal("09:00", HoursInput.ToWrite(hours)[1].Periods[0].Opens);
    }
    [Theory]
    [InlineData("2026-01-05T06:00:00Z", "2026-01-05T09:00:00+02:00")]
    [InlineData("2026-07-06T05:00:00Z", "2026-07-06T09:00:00+03:00")]
    public void Cairo_uses_the_installed_winter_and_summer_offsets(string instant, string expected)
    {
        var next = Hours(1, new OpeningPeriodWrite("09:00", "17:00")).At(DateTimeOffset.Parse(instant)).NextOpen!.Value;
        Assert.Equal(DateTimeOffset.Parse(expected), next); Assert.Equal(DateTimeOffset.Parse(expected).Offset, next.Offset);
    }
    [Fact]
    public void Cairo_spring_gap_moves_nonexistent_boundaries_to_the_first_existing_minute()
    {
        var hours = Hours(5, new OpeningPeriodWrite("00:30", "02:00")); var before = hours.At(DateTimeOffset.Parse("2026-04-23T21:59:59Z"));
        Assert.False(before.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-04-24T01:00:00+03:00"), before.NextOpen);
        var open = hours.At(DateTimeOffset.Parse("2026-04-23T22:00:00Z")); Assert.True(open.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-04-24T02:00:00+03:00"), open.ClosesAt);
        var nonexistent = Hours(5, new OpeningPeriodWrite("00:10", "00:50")).At(DateTimeOffset.Parse("2026-04-23T21:59:59Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T00:10:00+03:00"), nonexistent.NextOpen);
        var precedingWeekend = Hours(5, new OpeningPeriodWrite("00:10", "00:50")).At(DateTimeOffset.Parse("2026-04-18T10:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T00:10:00+03:00"), precedingWeekend.NextOpen);
    }
    [Fact]
    public void Cairo_repeated_hour_opens_on_first_occurrence_and_closes_on_last()
    {
        var hours = Hours(4, new OpeningPeriodWrite("23:15", "23:45"));
        var before = hours.At(DateTimeOffset.Parse("2026-10-29T20:00:00Z")); Assert.Equal(DateTimeOffset.Parse("2026-10-29T23:15:00+03:00"), before.NextOpen);
        Assert.True(hours.At(DateTimeOffset.Parse("2026-10-29T20:50:00Z")).OpenNow);
        var second = hours.At(DateTimeOffset.Parse("2026-10-29T21:30:00Z")); Assert.True(second.OpenNow); Assert.Equal(DateTimeOffset.Parse("2026-10-29T23:45:00+02:00"), second.ClosesAt);
        Assert.False(hours.At(DateTimeOffset.Parse("2026-10-29T21:45:00Z")).OpenNow);
    }
}
