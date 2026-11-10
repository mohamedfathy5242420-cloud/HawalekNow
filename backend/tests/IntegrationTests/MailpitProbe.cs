using System.Net.Http.Json;
using System.Text.Json;

namespace HawalekNow.IntegrationTests;

// Reads only this test's random recipient. Never deletes shared messages or emits their contents.
public static class MailpitProbe
{
    public static int SmtpPort => int.Parse(Environment.GetEnvironmentVariable("HAWALEKNOW_TEST_SMTP_PORT")
        ?? throw new InvalidOperationException("Configure the isolated Mailpit SMTP port."), System.Globalization.CultureInfo.InvariantCulture);

    public static async Task<string[]> MessagesAsync(string recipient)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("HAWALEKNOW_TEST_MAILPIT_URL")
            ?? throw new InvalidOperationException("Configure the isolated Mailpit API URL."))
        };
        using var listing = await client.GetFromJsonAsync<JsonDocument>("/api/v1/messages?limit=500");
        var ids = listing!.RootElement.GetProperty("messages").EnumerateArray()
            .Where(message => message.GetProperty("To").EnumerateArray().Any(to => to.GetProperty("Address").GetString() == recipient))
            .Select(message => message.GetProperty("ID").GetString()).ToArray();
        var texts = new List<string>();
        foreach (var id in ids)
        {
            using var detail = await client.GetFromJsonAsync<JsonDocument>("/api/v1/message/" + id);
            texts.Add(detail!.RootElement.GetProperty("Text").GetString()!);
        }
        return texts.ToArray();
    }
}
