using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

public class SkillsController : BaseApiController
{
    private readonly ISkillService _skillService;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(ISkillService skillService, ILogger<SkillsController> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new skill in the taxonomy (Admin only).
    /// </summary>
    /// <param name="request">Skill details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created skill</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<SkillResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SkillResponse>>> CreateSkill(
        [FromBody] CreateSkillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _skillService.CreateSkillAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lists all available skills in the taxonomy (Public).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of skills</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<SkillResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<SkillResponse>>>> GetAllSkills(CancellationToken cancellationToken)
    {
        var result = await _skillService.GetAllSkillsAsync(cancellationToken);
        return Ok(result);
    }
}
