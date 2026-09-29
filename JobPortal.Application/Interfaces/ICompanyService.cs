using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Company;

namespace JobPortal.Application.Interfaces;

public interface ICompanyService
{
    Task<ApiResponse<CompanyResponse>> CreateCompanyAsync(CompanyCreateRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyResponse>> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyResponse>> UpdateCompanyAsync(Guid id, CompanyUpdateRequest request, CancellationToken cancellationToken = default);
}
