namespace RAG_AI_API.DTOs;

public sealed record DocumentChunkResult(Guid ChunkId, Guid DocumentId, string DocumentName, string Content, string? Heading, int PageNumber, double Similarity);
