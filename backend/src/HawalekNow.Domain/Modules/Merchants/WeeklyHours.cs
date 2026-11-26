using System.Collections.Immutable;

namespace HawalekNow.Domain.Modules.Merchants;

public sealed record OpeningPeriod(TimeOnly Opens, TimeOnly Closes, bool ClosesNextDay);
public sealed record WorkingDay(int Day, bool Closed, ImmutableArray<OpeningPeriod> Periods);
public sealed record OpeningState(bool OpenNow, DateTimeOffset? NextOpen, DateTimeOffset? ClosesAt, string TimeZone);

public sealed class WeeklyHours
{
    public const string TimeZoneId = "Africa/Cairo";
    private const int WeekMinutes = 7 * 1440;
    public ImmutableArray<WorkingDay> Days { get; }
    private WeeklyHours(ImmutableArray<WorkingDay> days) => Days = days;
    public static WeeklyHours AllClosed { get; } = new(Enumerable.Range(0, 7).Select(day => new WorkingDay(day, true, [])).ToImmutableArray());

    public static bool TryCreate(IReadOnlyList<WorkingDay>? days, out WeeklyHours? hours)
    {
        hours = null;
        if (days is null || days.Count != 7 || days.Any(day => day is null || day.Day is < 0 or > 6 || day.Periods.IsDefault) || days.Select(day => day.Day).Distinct().Count() != 7) return false;
        var intervals = new List<(int Start, int End)>();
        foreach (var day in days)
        {
            if (day.Closed ? day.Periods.Length != 0 : day.Periods.Length is < 1 or > 8) return false;
            foreach (var period in day.Periods)
            {
                if (period is null || period.Opens.Ticks % TimeSpan.TicksPerMinute != 0 || period.Closes.Ticks % TimeSpan.TicksPerMinute != 0) return false;
                var start = (int)period.Opens.ToTimeSpan().TotalMinutes; var end = (int)period.Closes.ToTimeSpan().TotalMinutes + (period.ClosesNextDay ? 1440 : 0);
                if (end <= start || end - start > 1440) return false;
                start += day.Day * 1440; end += day.Day * 1440;
                intervals.Add((start, Math.Min(end, WeekMinutes)));
                if (end > WeekMinutes) intervals.Add((0, end - WeekMinutes));
            }
        }
        var sorted = intervals.OrderBy(value => value.Start).ToArray();
        for (var i = 1; i < sorted.Length; i++) if (sorted[i].Start < sorted[i - 1].End) return false;
        hours = new(days.OrderBy(day => day.Day).Select(day => day with { Periods = day.Periods.OrderBy(period => period.Opens).ToImmutableArray() }).ToImmutableArray()); return true;
    }

    public OpeningState At(DateTimeOffset instant)
    {
        // Use the installed timezone database; no fixed UTC offset or host-local clock assumptions.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        if (Days.Sum(day => day.Periods.Sum(period => Duration(period))) == WeekMinutes) return new(true, null, null, TimeZoneId);
        var today = TimeZoneInfo.ConvertTime(instant, zone).Date;
        var intervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        // Include a second week: the next scheduled period may fall wholly inside a DST gap.
        for (var offset = -1; offset <= 15; offset++)
        {
            var date = today.AddDays(offset);
            foreach (var period in Days[(int)date.DayOfWeek].Periods)
            {
                var start = Resolve(date.Add(period.Opens.ToTimeSpan()), zone, true);
                var end = Resolve(date.AddDays(period.ClosesNextDay ? 1 : 0).Add(period.Closes.ToTimeSpan()), zone, false);
                if (end > start) intervals.Add((start, end));
            }
        }
        var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var interval in intervals.OrderBy(value => value.Start))
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End) merged[^1] = (merged[^1].Start, interval.End > merged[^1].End ? interval.End : merged[^1].End);
            else merged.Add(interval);
        }
        foreach (var interval in merged)
        {
            if (interval.Start <= instant && instant < interval.End) return new(true, null, TimeZoneInfo.ConvertTime(interval.End, zone), TimeZoneId);
            if (interval.Start > instant) return new(false, TimeZoneInfo.ConvertTime(interval.Start, zone), null, TimeZoneId);
        }
        return new(false, null, null, TimeZoneId);
    }

    private static int Duration(OpeningPeriod period) => (int)(period.Closes.ToTimeSpan() - period.Opens.ToTimeSpan()).TotalMinutes + (period.ClosesNextDay ? 1440 : 0);
    private static DateTimeOffset Resolve(DateTime local, TimeZoneInfo zone, bool opening)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            return new DateTimeOffset(local, opening ? offsets.Max() : offsets.Min()).ToUniversalTime();
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
}
