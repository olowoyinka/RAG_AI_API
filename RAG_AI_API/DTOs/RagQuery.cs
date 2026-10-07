namespace RAG_AI_API.DTOs;

public sealed record RagQuery(Guid KnowledgeBaseId, string Question, int TopK = 20, int MaxContextChunks = 5);

