using Microsoft.EntityFrameworkCore;
using RAG_AI_API.Models;

namespace RAG_AI_API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeBase>(e =>
        {
            e.ToTable("knowledge_bases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.KnowledgeBaseId, x.ContentHash });
            e.HasOne(x => x.KnowledgeBase)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.KnowledgeBaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.ToTable("document_chunks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Embedding).HasColumnType("vector(384)");
            e.Property(x => x.EmbeddingModel).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
            e.HasIndex(x => x.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);
            e.HasOne(x => x.Document)
                .WithMany(x => x.Chunks)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
