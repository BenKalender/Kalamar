using System.Net;
using System.Net.Http.Json;

namespace Frontend.Models;

/// <summary>Backend'in döndürdüğü RFC 7807 ProblemDetails / ValidationProblemDetails gövdesi.</summary>
public sealed class ApiProblem
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>Başarısız bir cevaptan kullanıcıya gösterilecek mesajı üretir.</summary>
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        ApiProblem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>();
        }
        catch
        {
            // Gövde ProblemDetails değilse yalnızca durum koduyla devam et.
        }

        if (problem?.Errors is { Count: > 0 } errors)
        {
            return string.Join(" ", errors.SelectMany(e => e.Value));
        }

        var text = problem?.Detail ?? problem?.Title;
        return text ?? $"Request failed ({(int)response.StatusCode} {response.StatusCode}).";
    }

    public static bool IsServiceUnavailable(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;
}
