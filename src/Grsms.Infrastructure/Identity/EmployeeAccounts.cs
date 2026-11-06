using Grsms.Application.Common;
using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Identity;

/// <inheritdoc cref="IEmployeeAccounts"/>
public class EmployeeAccounts(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : IEmployeeAccounts
{
    public async Task<Result> ActivateAsStaffAsync(string userId, string staffNumber, int departmentId, int siteId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("The new employee's account was not found.");
        staffNumber = staffNumber.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.StaffNumber == staffNumber && u.Id != userId, ct))
            return Result.Failure($"Staff number {staffNumber} is already used by another account.");

        user.StaffNumber = staffNumber;
        user.DepartmentId = departmentId;
        user.SiteId = siteId;
        user.IsCandidate = false;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Result.Failure([.. updated.Errors.Select(e => e.Description)]);

        if (!await userManager.IsInRoleAsync(user, Roles.Employee))
        {
            var added = await userManager.AddToRoleAsync(user, Roles.Employee);
            if (!added.Succeeded) return Result.Failure([.. added.Errors.Select(e => e.Description)]);
        }
        if (await userManager.IsInRoleAsync(user, Roles.Candidate))
        {
            var removed = await userManager.RemoveFromRoleAsync(user, Roles.Candidate);
            if (!removed.Succeeded) return Result.Failure([.. removed.Errors.Select(e => e.Description)]);
        }
        return Result.Success();
    }
}
