using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RAG_AI_API.Services;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
}


public class EmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _client;
    private readonly HuggingFaceOptions _huggingFaceOptions;
    private readonly RagOptions _ragOptions;

    public EmbeddingService(IHttpClientFactory client,
                            IOptions<HuggingFaceOptions> huggingFaceOptions,
                            IOptions<RagOptions> ragOptions)
    {
        _client = client;
        _huggingFaceOptions = huggingFaceOptions.Value;
        _ragOptions = ragOptions.Value;
    }


    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0) return [];

        var output = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(Math.Max(1, _huggingFaceOptions.BatchSize)))
        {
            var request = new
            {
                inputs = batch,
                options = new { wait_for_model = true }
            };

            using var client = _client.CreateClient("huggingface");
            using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_huggingFaceOptions.ApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _huggingFaceOptions.ApiKey);

            using var response = await client.PostAsync(_huggingFaceOptions.Endpoint, content, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Embedding provider failed ({(int)response.StatusCode}): {body}");

            using var json = JsonDocument.Parse(body);

            output.AddRange(ParseEmbeddingResponse(json.RootElement, batch.Length));
        }

        foreach (var vector in output)
        {
            if (vector.Length != _ragOptions.EmbeddingDimensions)
                throw new InvalidOperationException($"Embedding dimension {vector.Length} does not match configured dimension {_ragOptions.EmbeddingDimensions}.");
        }

        return output;
    }


    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var result = await GenerateEmbeddingsAsync([text], cancellationToken);

        return result[0];
    }


    private static IReadOnlyList<float[]> ParseEmbeddingResponse(JsonElement root, int batchSize)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Embedding response is not an array.");

        if (root.GetArrayLength() == batchSize && root[0].ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(x => x.EnumerateArray().Select(v => v.GetSingle()).ToArray())
                       .ToList();
        }

        if (batchSize == 1 && root[0].ValueKind == JsonValueKind.Number)
            return [root.EnumerateArray().Select(v => v.GetSingle()).ToArray()];

        if (root.GetArrayLength() == batchSize && root[0].ValueKind == JsonValueKind.Array && root[0].GetArrayLength() > 0 && root[0][0].ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray()
                       .Select(item =>
                       {
                           var tokenVectors = item.EnumerateArray().Select(t => t.EnumerateArray().Select(v => v.GetSingle()).ToArray())
                                                  .ToList();

                            return MeanPool(tokenVectors);
                       })
                       .ToList();
        }

        throw new InvalidOperationException("Unsupported Hugging Face embedding response shape.");
    }


    private static float[] MeanPool(IReadOnlyList<float[]> vectors)
    {
        var dimensions = vectors[0].Length;
        var result = new float[dimensions];

        foreach (var vector in vectors)
            for (var i = 0; i < dimensions; i++)
                result[i] += vector[i];

        for (var i = 0; i < dimensions; i++)
            result[i] /= vectors.Count;

        var norm = Math.Sqrt(result.Sum(x => x * x));

        if (norm > 0)
            for (var i = 0; i < dimensions; i++)
                result[i] = (float)(result[i] / norm);

        return result;
    }
}
