namespace RAG_AI_API.DTOs;

public sealed record GeneratedAnswer(string Text, int PromptTokens = 0, int CompletionTokens = 0);
