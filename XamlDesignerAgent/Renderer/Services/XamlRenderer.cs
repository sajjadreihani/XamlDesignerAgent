using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using XamlDesignerAgent.Renderer.Interface;
using XamlDesignerAgent.Renderer.Models;

namespace XamlDesignerAgent.Renderer.Services;

public class XamlRenderService(HttpClient http, ILogger<XamlRenderService> logger) : IXamlRenderer
{
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            var baseUri = http.BaseAddress?.ToString()?.TrimEnd('/') ?? "http://localhost:5099";
            var r = await http.GetAsync($"{baseUri}/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<RenderResult> RenderAsync(string xaml)
    {
        return await PostAsync<RenderResult>("/render", xaml)
            ?? new RenderResult(false, null, "Render failed");
    }

    public async Task<ValidationResult> ValidateAsync(string xaml)
    {
        return await PostAsync<ValidationResult>("/validate", xaml)
            ?? new ValidationResult(false, "Request failed", null, null);
    }

    public async Task<FormatResult> FormatAsync(string xaml)
    {
        return await PostAsync<FormatResult>("/format", xaml)
            ?? new FormatResult(false, null, "Request failed");
    }

    private async Task<T?> PostAsync<T>(string path, string body)
    {
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "text/plain");
            var baseUri = http.BaseAddress?.ToString()?.TrimEnd('/') ?? "http://localhost:5099";
            using var response = await http.PostAsync($"{baseUri}{path}", content);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("XamlRenderer not reachable at {Path}", path);
            return default;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PostAsync failed for {Path}", path);
            return default;
        }
    }
}