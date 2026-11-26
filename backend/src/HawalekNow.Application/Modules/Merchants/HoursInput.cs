using System.Collections.Immutable;
using System.Globalization;
using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.Application.Modules.Merchants;

public sealed record OpeningPeriodWrite(string Opens, string Closes, bool ClosesNextDay = false);
public sealed record WorkingDayWrite(int Day, bool Closed, IReadOnlyList<OpeningPeriodWrite> Periods);

public static class HoursInput
{
    public static bool Normalize(IReadOnlyList<WorkingDayWrite>? value, out WeeklyHours? hours)
    {
        hours = null;
        if (value is null || value.Count != 7) return false;
        var days = new List<WorkingDay>();
        foreach (var day in value)
        {
            if (day is null || day.Periods is null || day.Periods.Count > 8) return false;
            var periods = ImmutableArray.CreateBuilder<OpeningPeriod>();
            foreach (var period in day.Periods)
            {
                if (period is null || !TimeOnly.TryParseExact(period.Opens, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opens) ||
                    !TimeOnly.TryParseExact(period.Closes, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closes)) return false;
                periods.Add(new(opens, closes, period.ClosesNextDay));
            }
            days.Add(new(day.Day, day.Closed, periods.ToImmutable()));
        }
        return WeeklyHours.TryCreate(days, out hours);
    }
    public static ImmutableArray<WorkingDayWrite> ToWrite(WeeklyHours hours) => hours.Days.Select(day => new WorkingDayWrite(day.Day, day.Closed,
        day.Periods.Select(period => new OpeningPeriodWrite(period.Opens.ToString("HH:mm", CultureInfo.InvariantCulture), period.Closes.ToString("HH:mm", CultureInfo.InvariantCulture), period.ClosesNextDay)).ToImmutableArray())).ToImmutableArray();
}
