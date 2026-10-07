namespace RAG_AI_API.DTOs;

public sealed record ExtractedDocument(string FileName, IReadOnlyList<ExtractedPage> Pages);
