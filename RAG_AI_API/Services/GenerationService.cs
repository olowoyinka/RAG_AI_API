using Microsoft.Extensions.Options;
using RAG_AI_API.DTOs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RAG_AI_API.Services;


public interface IGenerationService
{
    Task<GeneratedAnswer> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}


public class GenerationService : IGenerationService
{
    private readonly IHttpClientFactory _client;
    private readonly OllamaOptions _ollamaOptions;

    public GenerationService(IHttpClientFactory client,
                             IOptions<OllamaOptions> ollamaOptions)
    {
        _client = client;
        _ollamaOptions = ollamaOptions.Value;
    }


    public async Task<GeneratedAnswer> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        using var client = _client.CreateClient("ollama");

        var payload = new
        {
            model = _ollamaOptions.Model,
            stream = false,
            options = new { temperature = _ollamaOptions.Temperature },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_ollamaOptions.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ollamaOptions.ApiKey);

        using var response = await client.PostAsync("chat/completions", content, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);

        var text = json.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Ollama returned an empty answer.");

        var promptTokens = json.RootElement.TryGetProperty("prompt_eval_count", out var p) ? p.GetInt32() : 0;

        var completionTokens = json.RootElement.TryGetProperty("eval_count", out var c) ? c.GetInt32() : 0;

        return new GeneratedAnswer(text, promptTokens, completionTokens);
    }
}
