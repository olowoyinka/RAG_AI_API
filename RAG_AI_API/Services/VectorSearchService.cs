using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using RAG_AI_API.Data;
using RAG_AI_API.DTOs;
using RAG_AI_API.Models;

namespace RAG_AI_API.Services;


public interface IVectorSearchService
{
    Task<IReadOnlyList<DocumentChunkResult>> SearchAsync(Guid tenantId, Guid knowledgeBaseId, float[] queryEmbedding, int topK, CancellationToken cancellationToken);
}


public class VectorSearchService : IVectorSearchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RagOptions _options;

    public VectorSearchService(ApplicationDbContext dbContext,
                                IOptions<RagOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }


    public async Task<IReadOnlyList<DocumentChunkResult>> SearchAsync(Guid tenantId, Guid knowledgeBaseId, float[] queryEmbedding, int topK, CancellationToken cancellationToken)
    {
        var vector = new Vector(queryEmbedding);

        var rows = await _dbContext.Database.SqlQueryRaw<SearchRow>(
                                    """
                                        SELECT
                                            dc."Id" AS "ChunkId",
                                            dc."DocumentId" AS "DocumentId",
                                            d."FileName" AS "DocumentName",
                                            dc."Content" AS "Content",
                                            dc."Heading" AS "Heading",
                                            dc."PageNumber" AS "PageNumber",
                                            1 - (dc."Embedding" <=> {0}) AS "Similarity"
                                        FROM "document_chunks" dc
                                        INNER JOIN "documents" d ON d."Id" = dc."DocumentId"
                                        WHERE dc."TenantId" = {1}
                                          AND d."KnowledgeBaseId" = {2}
                                          AND d."Status" = 'Completed'
                                        ORDER BY dc."Embedding" <=> {0}
                                        LIMIT {3}
                                    """,
                                   vector, tenantId, knowledgeBaseId, Math.Clamp(topK, 1, 100))
                                   .AsNoTracking()
                                   .ToListAsync(cancellationToken);

        return rows
            .Where(x => x.Similarity >= _options.MinimumSimilarity)
            .Select(x => new DocumentChunkResult(
                x.ChunkId,
                x.DocumentId,
                x.DocumentName,
                x.Content,
                x.Heading,
                x.PageNumber,
                x.Similarity))
            .ToList();
    }
}
