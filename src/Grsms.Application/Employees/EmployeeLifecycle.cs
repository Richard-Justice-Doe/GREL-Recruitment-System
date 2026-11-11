using Grsms.Application.Common.Interfaces;
using Grsms.Application.Onboarding;
using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Probation;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;
using Microsoft.Extensions.Logging;

namespace Grsms.Application.Employees;

/// <summary>
/// Phase 28 (docs/30): where the existing workflows hand over to the employee record. Additive only:
///   * <see cref="OnActivatedAsync"/> is the last step of activating an onboarding (OnboardingService.ActivateAsync);
///   * <see cref="OnConfirmedAsync"/> is the last step of confirming a probation (ProbationService);
///   * <see cref="BackfillAsync"/> runs at start-up and creates the record of anyone activated before Phase 28.
/// The hooks run after the existing work has been saved and never throw: a failure is logged and the next start-up's
/// backfill creates whatever is missing, so an activation or a confirmation can never fail because of them.
/// </summary>
public class EmployeeLifecycle(
    IRepository<Employee> employees,
    IRepository<EmployeeAssignment> assignments,
    IRepository<OnboardingRecord> onboardings,
    IRepository<Vacancy> vacancies,
    IRepository<ProbationRecord> probations,
    IRepository<ConfirmationRecord> confirmations,
    PersonnelFileService personnelFiles,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    ILogger<EmployeeLifecycle> logger)
{
    /// <summary>The onboarding was activated: create the employee (On probation), or bring back a returning one.</summary>
    public async Task OnActivatedAsync(OnboardingRecord o, CancellationToken ct = default)
    {
        try
        {
            await FromOnboardingAsync(o, EmployeeStatus.OnProbation, null, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not create the employee record for onboarding {Number}; the next start-up will retry.", o.Number);
        }
    }

    /// <summary>The probation was confirmed: the employee becomes Active from the confirmation date.</summary>
    public async Task OnConfirmedAsync(string userId, DateOnly confirmationDate, CancellationToken ct = default)
    {
        try
        {
            if ((await employees.ListAsync(e => e.UserId == userId, ct)).FirstOrDefault() is not { } e || e.Status == EmployeeStatus.Separated) return;
            (e.Status, e.ConfirmationDate, e.StatusChangedAt, e.Version) = (EmployeeStatus.Active, confirmationDate, DateTime.UtcNow, Guid.NewGuid());
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not mark employee {UserId} as confirmed; set it on the employee page.", userId);
        }
    }

    /// <summary>
    /// Creates the employee record of every onboarding that was activated without one (before Phase 28, or when a hook failed),
    /// and files the offer letter and interview results in their personnel file. Safe to run any number of times. Returns how
    /// many records it created.
    /// </summary>
    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var activated = await onboardings.ListAsync(o => (o.Status == OnboardingStatus.AwaitingDepartmentalInduction || o.Status == OnboardingStatus.Completed)
                                                         && o.EmployeeNumber != null, ct);
        if (activated.Count == 0) return 0;
        // docs/32: only the records of these people, not the whole Employees table.
        var activatedUsers = activated.Select(o => o.EmployeeUserId).Distinct().ToList();
        var users = (await employees.ListAsync(e => activatedUsers.Contains(e.UserId), ct)).Select(e => e.UserId).ToHashSet();
        var created = 0;
        foreach (var o in activated.Where(o => !users.Contains(o.EmployeeUserId)).OrderBy(o => o.CompletedAt ?? o.StatusChangedAt))
        {
            try
            {
                var probation = (await probations.ListAsync(p => p.OnboardingRecordId == o.Id, ct)).FirstOrDefault();
                var confirmed = probation?.Status == ProbationStatus.Confirmed
                    ? (await confirmations.ListAsync(c => c.ProbationRecordId == probation.Id, ct)).FirstOrDefault()?.ConfirmationDate ?? probation.EndDate
                    : (DateOnly?)null;
                if (await FromOnboardingAsync(o, confirmed is null ? EmployeeStatus.OnProbation : EmployeeStatus.Active, confirmed, ct) is null) continue;
                await unitOfWork.SaveChangesAsync(ct);
                users.Add(o.EmployeeUserId);
                created++;
                await personnelFiles.OpenForOnboardingAsync(o, ct, includeInductionRecord: o.Status == OnboardingStatus.Completed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not create the employee record for onboarding {Number}.", o.Number);
                return created; // the context may hold the failed change; the next start-up carries on
            }
        }
        if (created > 0) logger.LogInformation("Created {Count} employee record(s) for employees activated before Phase 28.", created);
        return created;
    }

    /// <summary>Adds (or updates) the employee from an activated onboarding, with a "Hired" job history line. Not saved yet.</summary>
    private async Task<Employee?> FromOnboardingAsync(OnboardingRecord o, EmployeeStatus status, DateOnly? confirmed, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.EmployeeNumber)) return null;
        var v = await vacancies.GetByIdAsync(o.VacancyId, ct);
        var type = v?.EmploymentType ?? EmploymentType.Permanent;
        var reportsTo = v?.ReportingManagerUserId is { } m && m != o.EmployeeUserId ? m : null;
        var now = DateTime.UtcNow;

        var e = (await employees.ListAsync(x => x.UserId == o.EmployeeUserId, ct)).FirstOrDefault();
        if (e is null)
        {
            if (await employees.AnyAsync(x => x.EmployeeNumber == o.EmployeeNumber, ct))
            {
                logger.LogWarning("Staff number {Number} of onboarding {Onboarding} already belongs to another employee record.", o.EmployeeNumber, o.Number);
                return null;
            }
            e = new Employee { UserId = o.EmployeeUserId, HireDate = o.StartDate };
            employees.Add(e);
        }
        else
        {
            if (e.OnboardingRecordId == o.Id) return null; // already done
            if (e.Status == EmployeeStatus.Separated) e.HireDate = o.StartDate; // rehired
            if ((await assignments.ListAsync(a => a.EmployeeId == e.Id && a.EffectiveTo == null, ct)).FirstOrDefault() is { } open)
                open.EffectiveTo = o.StartDate > open.EffectiveFrom ? o.StartDate.AddDays(-1) : open.EffectiveFrom;
        }

        e.EmployeeNumber = o.EmployeeNumber;
        e.OnboardingRecordId = o.Id;
        (e.PositionTitle, e.DepartmentId, e.SiteId, e.ReportsToUserId, e.EmploymentType) = (o.JobTitle, o.DepartmentId, o.SiteId, reportsTo, type);
        (e.Status, e.ConfirmationDate, e.StatusChangedAt, e.Version) = (status, confirmed, now, Guid.NewGuid());
        e.Assignments.Add(new EmployeeAssignment
        {
            EffectiveFrom = o.StartDate, PositionTitle = o.JobTitle, DepartmentId = o.DepartmentId, SiteId = o.SiteId, GradeId = e.GradeId,
            ReportsToUserId = reportsTo, EmploymentType = type, Source = AssignmentSource.Hire, SourceId = o.Id,
            Note = $"Onboarding {o.Number}", RecordedByUserId = me.UserId
        });
        return e;
    }
}
