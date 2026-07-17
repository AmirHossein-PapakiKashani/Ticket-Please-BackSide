using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Domain;

namespace Ticket.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/v1/admin")]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("dashboard/stats")]
    public Task<AdminDashboardStats> GetDashboardStats(CancellationToken cancellationToken) => adminService.GetDashboardStatsAsync(cancellationToken);

    [HttpGet("providers")]
    public async Task<ActionResult<PagedResponse<ProviderListItem>>> GetProviders(string? search, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var error = ValidatePaging(pageNumber, pageSize); if (error is not null) return error;
        return Ok(await adminService.GetProvidersAsync(search, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("providers")]
    public async Task<ActionResult<CreatedProviderResponse>> CreateProvider(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request); if (error is not null) return error;
        var created = await adminService.CreateProviderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProvider), new { providerId = created.ProviderId }, created);
    }

    [HttpPatch("providers/{providerId:int}/deactivate")]
    public async Task<SuccessResponse> DeactivateProvider(int providerId, CancellationToken cancellationToken) => await adminService.DeactivateProviderAsync(providerId, cancellationToken);

    [HttpGet("providers/{providerId:int}")]
    public async Task<ActionResult<ProviderDetail>> GetProvider(int providerId, CancellationToken cancellationToken) => Ok(await adminService.GetProviderAsync(providerId, cancellationToken));

    [HttpPut("providers/{providerId:int}")]
    public async Task<ActionResult<SuccessResponse>> UpdateProvider(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request); if (error is not null) return error;
        return Ok(await adminService.UpdateProviderAsync(providerId, request, cancellationToken));
    }

    [HttpGet("plans")]
    public async Task<ActionResult<PagedResponse<PlanListItem>>> GetPlans(string? search, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var error = ValidatePaging(pageNumber, pageSize); if (error is not null) return error;
        return Ok(await adminService.GetPlansAsync(search, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("plans")]
    public async Task<ActionResult<CreatedPlanResponse>> CreatePlan(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request); if (error is not null) return error;
        var created = await adminService.CreatePlanAsync(request, cancellationToken);
        return CreatedAtAction(nameof(UpdatePlan), new { planId = created.PlanId }, created);
    }

    [HttpPut("plans/{planId:int}")]
    public async Task<ActionResult<SuccessResponse>> UpdatePlan(int planId, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request); if (error is not null) return error;
        return Ok(await adminService.UpdatePlanAsync(planId, request, cancellationToken));
    }

    [HttpPatch("plans/{planId:int}/deactivate")]
    public async Task<SuccessResponse> DeactivatePlan(int planId, CancellationToken cancellationToken) => await adminService.DeactivatePlanAsync(planId, cancellationToken);

    [HttpGet("providers/{providerId:int}/subscriptions")]
    public async Task<ActionResult<PagedResponse<SubscriptionListItem>>> GetSubscriptions(int providerId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var error = ValidatePaging(pageNumber, pageSize); if (error is not null) return error;
        return Ok(await adminService.GetSubscriptionsAsync(providerId, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("providers/{providerId:int}/subscriptions")]
    public async Task<ActionResult<CreatedSubscriptionResponse>> CreateSubscription(int providerId, CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request); if (error is not null) return error;
        return StatusCode(StatusCodes.Status201Created, await adminService.CreateSubscriptionAsync(providerId, request, cancellationToken));
    }

    private ActionResult? ValidatePaging(int pageNumber, int pageSize) => pageNumber < 1 ? ValidationErrors(new Dictionary<string, string[]> { ["pageNumber"] = ["pageNumber must be at least 1."] }) : pageSize is < 1 or > 100 ? ValidationErrors(new Dictionary<string, string[]> { ["pageSize"] = ["pageSize must be between 1 and 100."] }) : null;
    private ActionResult? Validate(CreateProviderRequest r) => Errors(("name", string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length is < 2 or > 200, "Name must be 2 to 200 characters."), ("email", !Email(r.Email), "Email must be valid."), ("phoneNumber", string.IsNullOrWhiteSpace(r.PhoneNumber), "Phone number is required."), ("managerUsername", r.ManagerUsername?.Trim().Length is < 3 or > 100, "Manager username must be 3 to 100 characters."), ("managerFullName", string.IsNullOrWhiteSpace(r.ManagerFullName), "Manager full name is required."), ("managerPassword", r.ManagerPassword?.Length < 6, "Manager password must be at least 6 characters."));
    private ActionResult? Validate(UpdateProviderRequest r) => Errors(("name", string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length is < 2 or > 200, "Name must be 2 to 200 characters."), ("email", !Email(r.Email), "Email must be valid."), ("phoneNumber", string.IsNullOrWhiteSpace(r.PhoneNumber), "Phone number is required."));
    private ActionResult? Validate(CreatePlanRequest r) => ValidatePlan(r.Name, r.DurationDays, r.Price, r.MaxClientCount, r.MaxAgentCount, r.ModulesJson);
    private ActionResult? Validate(UpdatePlanRequest r) => ValidatePlan(r.Name, r.DurationDays, r.Price, r.MaxClientCount, r.MaxAgentCount, r.ModulesJson);
    private ActionResult? Validate(CreateSubscriptionRequest r) => Errors(("planId", r.PlanId < 1, "planId must be positive."), ("moneyPaid", r.MoneyPaid < 0, "moneyPaid must be non-negative."));
    private ActionResult? ValidatePlan(string name, int durationDays, decimal price, int maxClientCount, int maxAgentCount, string? modulesJson) => Errors(("name", string.IsNullOrWhiteSpace(name), "Name is required."), ("durationDays", durationDays <= 0, "durationDays must be greater than zero."), ("price", price < 0, "price must be non-negative."), ("maxClientCount", maxClientCount < 1, "maxClientCount must be at least 1."), ("maxAgentCount", maxAgentCount < 1, "maxAgentCount must be at least 1."), ("modulesJson", !ValidJson(modulesJson), "modulesJson must be valid JSON."));
    private ActionResult? Errors(params (string Field, bool Invalid, string Error)[] checks) { var errors = checks.Where(c => c.Invalid).ToDictionary(c => c.Field, c => new[] { c.Error }); return errors.Count == 0 ? null : ValidationErrors(errors); }
    private ActionResult ValidationErrors(IDictionary<string, string[]> errors) => BadRequest(new ValidationProblemDetails(errors));
    private static bool Email(string? value) => !string.IsNullOrWhiteSpace(value) && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
    private static bool ValidJson(string? value) { if (string.IsNullOrWhiteSpace(value)) return true; try { JsonDocument.Parse(value); return true; } catch (JsonException) { return false; } }
}
