namespace RAG_AI_API.DTOs;

public sealed record TextChunk(int Index, string Content, string? Heading, int PageNumber, int TokenCount);
