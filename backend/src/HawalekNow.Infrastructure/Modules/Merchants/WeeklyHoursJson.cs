using System.Collections.Immutable;
using System.Text.Json;
using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public static class WeeklyHoursJson
{
    public static string Write(WeeklyHours hours) => JsonSerializer.Serialize(hours.Days);
    public static WeeklyHours Read(string json)
    {
        var days = JsonSerializer.Deserialize<ImmutableArray<WorkingDay>>(json);
        return WeeklyHours.TryCreate(days, out var hours) ? hours! : throw new InvalidDataException("Stored weekly hours are invalid.");
    }
}
