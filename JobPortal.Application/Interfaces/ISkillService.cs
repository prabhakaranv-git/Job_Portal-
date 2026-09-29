using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Skill;

namespace JobPortal.Application.Interfaces;

public interface ISkillService
{
    Task<ApiResponse<SkillResponse>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IEnumerable<SkillResponse>>> GetAllSkillsAsync(CancellationToken cancellationToken = default);
}
