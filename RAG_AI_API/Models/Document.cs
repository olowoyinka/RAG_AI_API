using RAG_AI_API.Enums;

namespace RAG_AI_API.Models;

public sealed class Document
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    
    public string FileName { get; set; } = null!;
    
    public string ContentType { get; set; } = null!;
    
    public long FileSize { get; set; }
    
    public string? StoragePath { get; set; }
    
    public string? ContentHash { get; set; }
    
    public int Version { get; set; } = 1;
    
    public DocumentStatusEnum Status { get; set; } = DocumentStatusEnum.Pending;
    
    public string? ErrorMessage { get; set; }
    
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? ProcessedOn { get; set; }

    public Guid KnowledgeBaseId { get; set; }
    public KnowledgeBase KnowledgeBase { get; set; } = null!;
    
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
