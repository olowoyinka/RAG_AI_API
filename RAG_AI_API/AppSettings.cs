namespace RAG_AI_API;


public sealed class RagOptions
{
    public int EmbeddingDimensions { get; set; } = 384;
    public string? EmbeddingModel { get; set; }
    public int ChunkTargetTokens { get; set; } = 500;
    public int ChunkMaximumTokens { get; set; } = 700;
    public int ChunkOverlapTokens { get; set; } = 75;
    public int VectorTopK { get; set; } = 20;
    public int FinalContextChunks { get; set; } = 5;
    public double MinimumSimilarity { get; set; } = 0.35;
}

public sealed class HuggingFaceOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public int BatchSize { get; set; } = 16;
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public double Temperature { get; set; } = 0.1;
}

public sealed class StorageOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
}

public sealed class RerankerOptions
{
    public bool UseExternalModel { get; set; } = false;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
}

