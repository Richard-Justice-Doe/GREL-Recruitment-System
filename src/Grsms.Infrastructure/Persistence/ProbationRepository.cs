using Grsms.Application.Probation;
using Grsms.Domain.Probation;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Phase 21: loads a probation with its objectives, development plan and history.</summary>
public class ProbationRepository(ApplicationDbContext context) : Repository<ProbationRecord>(context), IProbationRepository
{
    public Task<ProbationRecord?> GetFullAsync(int id, CancellationToken ct = default) =>
        Db.ProbationRecords
            .Include(p => p.Objectives)
            .Include(p => p.DevelopmentPlan)
            .Include(p => p.Actions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
}
