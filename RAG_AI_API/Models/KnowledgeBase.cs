namespace RAG_AI_API.Models;

public sealed class KnowledgeBase
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    
    public string Name { get; set; } = null!;
    
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Document> Documents { get; set; } = [];
}
