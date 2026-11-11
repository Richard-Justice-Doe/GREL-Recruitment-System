using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;

namespace Grsms.Application.Onboarding;

/// <summary>One induction area as used by the onboarding: its owner (null = HR), default flag and tasks.</summary>
public record InductionAreaRule(InductionArea Area, string? OwnerUserId, bool IsDefault, IReadOnlyList<string> Tasks);

/// <summary>One row of the Induction areas screen.</summary>
public record InductionAreaRowDto(InductionArea Area, string? OwnerUserId, string? OwnerName, bool IsDefault, string Tasks, bool TasksAreBuiltIn);

/// <summary>
/// Phase 20: the settings of the twelve induction areas (owner, ticked by default, tasks). Areas never saved use the
/// built-in values: no owner (HR), <see cref="OnboardingWorkflow.DefaultAreas"/> ticked, <see cref="OnboardingWorkflow.InductionTemplates"/>.
/// Corporate IT with no owner set falls back to the Head of the IT department, as before Phase 20.
///
/// Changing the settings needs "Maintain organisation data" (HR Administrator, Admin, Super Admin); it never changes
/// tasks already created.
/// </summary>
public class InductionAreaService(
    IRepository<InductionAreaSetting> settings,
    IRepository<Department> departments,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUserService me)
{
    private const int MaxTasks = 20, MaxTaskLength = 300;

    public bool CanManage => me.HasPermission(Permissions.ManageMasterData);

    /// <summary>The rules for every area, for the onboarding.</summary>
    public async Task<IReadOnlyDictionary<InductionArea, InductionAreaRule>> RulesAsync(CancellationToken ct = default)
    {
        var saved = (await settings.ListAsync(ct: ct)).ToDictionary(s => s.Area);
        var itHead = (await departments.ListAsync(d => d.Code == OnboardingWorkflow.ItDepartmentCode && d.IsActive, ct)).FirstOrDefault()?.HeadOfDepartmentUserId;
        return Enum.GetValues<InductionArea>().ToDictionary(a => a, a =>
        {
            var s = saved.GetValueOrDefault(a);
            var owner = s?.OwnerUserId ?? (a == InductionArea.CorporateIt ? itHead : null);
            return new InductionAreaRule(a, owner, s?.IsDefault ?? OnboardingWorkflow.DefaultAreas.Contains(a),
                s?.Tasks is { } custom ? OnboardingWorkflow.TaskLines(custom) : OnboardingWorkflow.InductionTemplates[a]);
        });
    }

    /// <summary>The Induction areas screen.</summary>
    public async Task<IReadOnlyList<InductionAreaRowDto>> GetAllAsync(CancellationToken ct = default)
    {
        var saved = (await settings.ListAsync(ct: ct)).ToDictionary(s => s.Area);
        var names = await users.GetDisplayNamesAsync(saved.Values.Select(s => s.OwnerUserId).OfType<string>(), ct);
        return Enum.GetValues<InductionArea>().Select(a =>
        {
            var s = saved.GetValueOrDefault(a);
            return new InductionAreaRowDto(a, s?.OwnerUserId, s?.OwnerUserId is { } o ? names.GetValueOrDefault(o) : null,
                s?.IsDefault ?? OnboardingWorkflow.DefaultAreas.Contains(a),
                s?.Tasks ?? string.Join("\n", OnboardingWorkflow.InductionTemplates[a]), s?.Tasks is null);
        }).ToList();
    }

    public Task<IReadOnlyList<LookupItem>> StaffAsync(CancellationToken ct = default) => users.GetActiveStaffAsync(null, ct);

    /// <summary>Saves one area's owner, default flag and tasks (one per line).</summary>
    public async Task<Result> SaveAsync(InductionArea area, string? ownerUserId, bool isDefault, string? tasks, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure("Only an HR Administrator, Admin or Super Admin can change the induction areas.");
        if (!Enum.IsDefined(area)) return Result.Failure("Unknown induction area.");
        ownerUserId = string.IsNullOrWhiteSpace(ownerUserId) ? null : ownerUserId.Trim();
        if (ownerUserId is not null && !await users.IsActiveStaffAsync(ownerUserId, ct)) return Result.Failure("The owner must be an active GREL staff member.");
        var lines = OnboardingWorkflow.TaskLines(tasks);
        if (lines.Count == 0) return Result.Failure("List at least one task (one per line).");
        if (lines.Count > MaxTasks) return Result.Failure($"List at most {MaxTasks} tasks.");
        if (lines.Any(l => l.Length > MaxTaskLength)) return Result.Failure($"Keep each task under {MaxTaskLength} characters.");

        var s = (await settings.ListAsync(x => x.Area == area, ct)).FirstOrDefault();
        if (s is null)
        {
            s = new InductionAreaSetting { Area = area };
            settings.Add(s);
        }
        s.OwnerUserId = ownerUserId;
        s.IsDefault = isDefault;
        var builtIn = OnboardingWorkflow.InductionTemplates[area];
        s.Tasks = lines.SequenceEqual(builtIn) ? null : string.Join("\n", lines); // unchanged = keep following the built-in list
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
