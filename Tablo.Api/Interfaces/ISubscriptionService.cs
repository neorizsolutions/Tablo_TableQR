using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces;

public interface ISubscriptionService
{
    Task<EffectiveAccessDto?> GetEffectiveAccessAsync(Guid restaurantId, Guid employeeId, Guid? outletId = null);
    Task<List<PlanDto>> GetPlansAsync();
    Task<PlanDto?> GetPlanByCodeAsync(string code);
    Task<PlanDto> UpdatePlanFeaturesAsync(Guid planId, UpdatePlanFeaturesRequestDto request, string actorEmail);
    Task<SubscriptionDto?> GetRestaurantSubscriptionAsync(Guid restaurantId);
    Task<SubscriptionDto> ChangeSubscriptionAsync(Guid restaurantId, ChangeSubscriptionRequestDto request, string actorEmail);
    Task<PlanChangePreviewDto> PreviewPlanChangeAsync(Guid restaurantId, string targetPlanCode);
    Task<List<SubscriptionAuditLogDto>> GetAuditLogsAsync(Guid? restaurantId = null, int limit = 100);
    Task<SuperAdminOverviewDto> GetSuperAdminOverviewAsync();
    Task<bool> HasCapabilityAsync(Guid restaurantId, string capability);
}
