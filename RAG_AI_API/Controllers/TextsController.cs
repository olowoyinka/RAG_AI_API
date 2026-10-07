using Microsoft.AspNetCore.Mvc;
using RAG_AI_API.DTOs;
using RAG_AI_API.Services;

namespace RAG_AI_API.Controllers;


[ApiController]
[Route("api/knowledge-bases/{knowledgeBaseId:guid}/texts")]
public class TextsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(IIngestionService _ingestion, [FromHeader] Guid tenantId, 
                                            [FromRoute] Guid knowledgeBaseId, CreateTextRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) 
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Title and text are required.");

        var documentId = await _ingestion.IngestTextAsync(tenantId, knowledgeBaseId, request.Title, request.Text, cancellationToken);

        return Accepted(new { documentId, status = "Completed" });
    }
}
