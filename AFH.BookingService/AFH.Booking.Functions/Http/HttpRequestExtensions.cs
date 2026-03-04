using Microsoft.Azure.Functions.Worker.Http;
using System.Text;
using System.Text.Json;


namespace AFH.Booking.Functions.Http;

public static class HttpRequestExtensions
{
    public static string? GetHeader(this HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    public static async Task<string> ReadBodyAsStringAsync(this HttpRequestData req, CancellationToken ct)
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    public static async Task<T?> ReadJsonAsync<T>(this HttpRequestData req, JsonSerializerOptions options, CancellationToken ct)
    {
        var raw = await req.ReadBodyAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(raw)) return default;

        return JsonSerializer.Deserialize<T>(raw, options);
    }
}
