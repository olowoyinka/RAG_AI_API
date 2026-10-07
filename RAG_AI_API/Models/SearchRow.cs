namespace RAG_AI_API.Models;


public sealed class SearchRow
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? Heading { get; set; }
    public int PageNumber { get; set; }
    public double Similarity { get; set; }
}
