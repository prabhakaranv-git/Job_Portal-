using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Company;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

public class CompaniesController : BaseApiController
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(ICompanyService companyService, ILogger<CompaniesController> logger)
    {
        _companyService = companyService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new company profile and associates the authenticated approved recruiter.
    /// </summary>
    /// <param name="request">Company details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created company</returns>
    [HttpPost]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyResponse>>> CreateCompany(
        [FromBody] CompanyCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateCompanyAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCompanyById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Retrieves a company profile by ID (Public).
    /// </summary>
    /// <param name="id">Company ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Company details</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyResponse>>> GetCompanyById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates a company profile (Approved owning recruiter only).
    /// </summary>
    /// <param name="id">Company ID</param>
    /// <param name="request">Updated company details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated company details</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyResponse>>> UpdateCompany(
        [FromRoute] Guid id,
        [FromBody] CompanyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateCompanyAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
