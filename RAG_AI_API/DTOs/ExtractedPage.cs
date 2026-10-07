namespace RAG_AI_API.DTOs;

public sealed record ExtractedPage(int PageNumber, string Text, string? Heading = null);