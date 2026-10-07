using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RAG_AI_API.Data;
using RAG_AI_API.Services;

namespace RAG_AI_API.Controllers;

[ApiController]
[Route("api/knowledge-bases/{knowledgeBaseId:guid}/documents")]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IIngestionService _ingestionService;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(ApplicationDbContext dbContext,
                               IIngestionService ingestionService,
                               IOptions<StorageOptions> storageOptions,
                               ILogger<DocumentsController> logger)
    {
        _dbContext = dbContext;
        _ingestionService = ingestionService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }


    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromHeader] Guid tenantId, [FromRoute] Guid knowledgeBaseId, IFormFile file, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) 
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        if (file.Length > _storageOptions.MaxFileSizeBytes)
            return BadRequest("File exceeds the configured maximum size.");

        var knowledgeBaseExists = await _dbContext.KnowledgeBases.AsNoTracking()
                                                  .AnyAsync(x => x.Id == knowledgeBaseId &&
                                                                 x.TenantId == tenantId &&
                                                                 x.IsActive,
                                                                 cancellationToken);

        if (!knowledgeBaseExists) 
            return NotFound("Knowledge base not found.");

        var documentId = await _ingestionService.IngestFileAsync(tenantId, knowledgeBaseId, file, cancellationToken);

        await _ingestionService.ProcessAsync(documentId, cancellationToken);

        return Accepted(new
        {
            documentId,
            status = "Completed"
        });
    }


    [HttpGet]
    public async Task<IActionResult> List([FromHeader] Guid tenantId, [FromRoute] Guid knowledgeBaseId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _dbContext.Documents.AsNoTracking()
                                     .Where(x => x.TenantId == tenantId &&
                                                 x.KnowledgeBaseId == knowledgeBaseId)
                                    .OrderByDescending(x => x.CreatedOn)
                                    .Select(x => new
                                    {
                                        x.Id,
                                        x.FileName,
                                        x.ContentType,
                                        x.FileSize,
                                        x.Status,
                                        x.Version,
                                        x.CreatedOn,
                                        x.ProcessedOn,
                                        x.ErrorMessage,
                                        ChunkCount = x.Chunks.Count
                                    })
                                    .ToListAsync(cancellationToken);

        return Ok(result);
    }


    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete([FromHeader] Guid tenantId, [FromRoute] Guid knowledgeBaseId, [FromRoute] Guid documentId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) 
            return Unauthorized();

        var document = await _dbContext.Documents
                                       .Where(x => x.Id == documentId &&
                                                   x.KnowledgeBaseId == knowledgeBaseId &&
                                                   x.TenantId == tenantId)
                                       .FirstOrDefaultAsync(cancellationToken);

        if (document is null) 
            return NotFound();

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
