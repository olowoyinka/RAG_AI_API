using Microsoft.AspNetCore.Mvc;
using RAG_AI_API.DTOs;
using RAG_AI_API.Services;

namespace RAG_AI_API.Controllers;


[ApiController]
[Route("api")]
public class ChatsController : ControllerBase
{

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(IRagService _ragService, [FromHeader] Guid tenantId, [FromBody] RagQuery request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) 
            return Unauthorized();

        var result = await _ragService.AskAsync(tenantId, request, cancellationToken);

        return Ok(result);
    }


    [HttpPost("search")]
    public async Task<IActionResult> Search(IEmbeddingService _embedding, IVectorSearchService _vectorSearch, 
                                            [FromHeader] Guid tenantId, [FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) 
            return Unauthorized();

        var vector = await _embedding.GenerateEmbeddingAsync(request.Query, cancellationToken);

        var results = await _vectorSearch.SearchAsync(tenantId, request.KnowledgeBaseId, vector, request.TopK, cancellationToken);

        return Ok(new SearchResponse(results));
    }
}
