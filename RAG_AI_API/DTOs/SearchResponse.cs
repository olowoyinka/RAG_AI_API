namespace RAG_AI_API.DTOs;


public sealed record SearchResponse(IReadOnlyList<DocumentChunkResult> Results);