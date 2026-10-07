namespace RAG_AI_API.DTOs;

public sealed record RagResponse(string Answer, IReadOnlyList<RagSource> Sources, RagUsage Usage);


public sealed record RagSource(Guid ChunkId, Guid DocumentId, string DocumentName, int PageNumber, double RetrievalScore, double RerankScore);


public sealed record RagUsage(int RetrievedChunks, int RerankedChunks, long DurationMs);
