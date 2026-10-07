namespace RAG_AI_API.DTOs;


public sealed record SearchRequest(Guid KnowledgeBaseId, string Query, int TopK = 20);