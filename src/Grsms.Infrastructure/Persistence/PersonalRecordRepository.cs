using Grsms.Application.Onboarding;
using Grsms.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Phase 19: loads a personal record with all its lists (languages, bank accounts, family, contacts, beneficiaries).</summary>
public class PersonalRecordRepository(ApplicationDbContext context)
    : Repository<EmployeePersonalRecord>(context), IPersonalRecordRepository
{
    public Task<EmployeePersonalRecord?> GetForOnboardingAsync(int onboardingId, CancellationToken ct = default) =>
        Db.EmployeePersonalRecords
            .Include(r => r.Languages)
            .Include(r => r.BankAccounts)
            .Include(r => r.Spouses)
            .Include(r => r.Children)
            .Include(r => r.Dependants)
            .Include(r => r.EmergencyContacts)
            .Include(r => r.Beneficiaries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.OnboardingRecordId == onboardingId, ct);
}
