using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using RAG_AI_API.Data;
using RAG_AI_API.DTOs;
using RAG_AI_API.Enums;
using RAG_AI_API.Models;
using System.Security.Cryptography;

namespace RAG_AI_API.Services;

public interface IIngestionService
{
    Task<Guid> IngestFileAsync(Guid tenantId, Guid knowledgeBaseId, IFormFile file, CancellationToken cancellationToken);

    Task ProcessAsync(Guid documentId, CancellationToken cancellationToken);

    Task<Guid> IngestTextAsync(Guid tenantId, Guid knowledgeBaseId, string title, string text, CancellationToken cancellationToken);
}


public class IngestionService : IIngestionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly ITextExtractionService _textExtractionService;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<IngestionService> _logger;
    private RagOptions _ragOptions;

    public IngestionService(ApplicationDbContext dbContext,
                            IStorageService storageService,
                            ITextExtractionService textExtractionService,
                            IChunkingService chunkingService,
                            IEmbeddingService embeddingService,
                            ILogger<IngestionService> logger,
                            IOptions<RagOptions> ragOptions)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _textExtractionService = textExtractionService;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _logger = logger;
        _ragOptions = ragOptions.Value;
    }


    public async Task<Guid> IngestFileAsync(Guid tenantId, Guid knowledgeBaseId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!_textExtractionService.CanHandle(file.ContentType, file.FileName))
            throw new NotSupportedException($"Unsupported file: {file.FileName}");

        var document = new Document
        {
            Id = Guid.CreateVersion7(DateTime.UtcNow),
            TenantId = tenantId,
            KnowledgeBaseId = knowledgeBaseId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            FileSize = file.Length,
            Status = DocumentStatusEnum.Pending
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            document.StoragePath = await _storageService.SaveFileAsync(file, cancellationToken);

            await using var stream = file.OpenReadStream();
            stream.Position = 0;
            document.ContentHash = await ComputeHashAsync(stream, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return document.Id;
        }
        catch
        {
            _dbContext.Entry(document).State = EntityState.Detached;
            throw;
        }
    }


    public async Task<Guid> IngestTextAsync(Guid tenantId, Guid knowledgeBaseId, string title, string text, CancellationToken cancellationToken)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KnowledgeBaseId = knowledgeBaseId,
            FileName = Path.GetFileName(title) + ".txt",
            ContentType = "text/plain",
            FileSize = text.Length,
            Status = DocumentStatusEnum.Processing
        };

        _dbContext.Documents.Add(document);

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var extracted = new ExtractedDocument(document.FileName, [new ExtractedPage(1, text)]);

            await CreateChunksAsync(document, extracted, cancellationToken);

            document.Status = DocumentStatusEnum.Completed;
            document.ProcessedOn = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return document.Id;
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatusEnum.Failed;
            document.ErrorMessage = ex.Message;

            await _dbContext.SaveChangesAsync(cancellationToken);

            throw;
        }
    }


    public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
                                       .Include(x => x.Chunks)
                                       .Where(x => x.Id == documentId)
                                       .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
            throw new InvalidOperationException("Document not found.");

        if (string.IsNullOrWhiteSpace(document?.StoragePath))
            throw new InvalidOperationException("Document has no stored file.");

        document.Status = DocumentStatusEnum.Processing;
        document.ErrorMessage = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var bytes = await _storageService.ReadFileAsync(document.StoragePath);
            await using var stream = new MemoryStream(bytes);

            var extracted = await _textExtractionService.ExtractAsync(stream, document.FileName, document.ContentType, cancellationToken);

            await CreateChunksAsync(document, extracted, cancellationToken);

            document.Status = DocumentStatusEnum.Completed;
            document.ProcessedOn = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingestion failed for {DocumentId}", documentId);

            document.Status = DocumentStatusEnum.Failed;
            document.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }


    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();

        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        
        return Convert.ToHexString(hash).ToLowerInvariant();
    }


    private async Task CreateChunksAsync(Document document, ExtractedDocument extracted, CancellationToken cancellationToken)
    {
        if (document.Chunks.Count > 0)
        {
            _dbContext.DocumentChunks.RemoveRange(document.Chunks);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var chunks = _chunkingService.Chunk(extracted);

        foreach (var batch in chunks.Chunk(32))
        {
            var texts = batch.Select(x => x.Content).ToList();

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

            for (var i = 0; i < batch.Length; i++)
            {
                var chunk = batch[i];

                var entity = new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    TenantId = document.TenantId,
                    DocumentId = document.Id,
                    ChunkIndex = chunk.Index,
                    Content = chunk.Content,
                    Heading = chunk.Heading,
                    TokenCount = chunk.TokenCount,
                    PageNumber = chunk.PageNumber,
                    EmbeddingModel = _ragOptions.EmbeddingModel,
                    EmbeddingDimensions = _ragOptions.EmbeddingDimensions,
                    Embedding = new Vector(embeddings[i])
                };

                _dbContext.DocumentChunks.Add(entity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
