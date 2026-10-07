using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RAG_AI_API.Data;
using RAG_AI_API.DTOs;
using RAG_AI_API.Models;

namespace RAG_AI_API.Controllers;

[ApiController]
[Route("api/knowledgebases")]
public class KnowledgeBasesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public KnowledgeBasesController(ApplicationDbContext dbContext)
    {
        this._dbContext = dbContext;
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromHeader] Guid tenantId, [FromBody] 
                                             CreateKnowledgeBaseRequest request, CancellationToken cancellationToken)
    {
        var entity = new KnowledgeBase
        {
            Id = Guid.CreateVersion7(DateTimeOffset.UtcNow),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedOn = DateTimeOffset.UtcNow
        };

        this._dbContext.KnowledgeBases.Add(entity);
        await this._dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, new
        {
            entity.Id,
            entity.Name,
            entity.Description
        });
    }


    [HttpGet]
    public async Task<IActionResult> List([FromHeader] Guid tenantId, CancellationToken cancellationToken)
    {
        return Ok(await this._dbContext.KnowledgeBases.AsNoTracking()
                                       .Where(x => x.TenantId == tenantId && 
                                                  x.IsActive)
                                       .OrderBy(x => x.Name)
                                       .Select(x => new 
                                       { 
                                          x.Id, 
                                          x.Name, 
                                          x.Description, 
                                          x.CreatedOn 
                                       })
                                       .ToListAsync(cancellationToken));
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromHeader] Guid tenantId, [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await this._dbContext.KnowledgeBases.AsNoTracking()
                                          .Where(x => x.Id == id && 
                                                      x.TenantId == tenantId)
                                          .Select(x => new 
                                          { 
                                             x.Id, 
                                             x.Name, 
                                             x.Description, 
                                             x.CreatedOn 
                                          })
                                          .SingleOrDefaultAsync(cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
