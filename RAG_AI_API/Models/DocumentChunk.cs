using Pgvector;

namespace RAG_AI_API.Models;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    
    public int ChunkIndex { get; set; }
    
    public string Content { get; set; } = null!;
    
    public string? Heading { get; set; }
    
    public int TokenCount { get; set; }
    
    public int PageNumber { get; set; }
    
    public string? MetadataJson { get; set; }
    
    public string EmbeddingModel { get; set; } = null!;
    
    public int EmbeddingDimensions { get; set; }
    
    public Vector Embedding { get; set; } = null!;
    
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
}