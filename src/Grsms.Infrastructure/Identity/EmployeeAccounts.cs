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

    public async Task<Result<string>> RegisterStaffAsync(string fullName, string email, string? phone, string staffNumber, int departmentId, int siteId,
        CancellationToken ct = default)
    {
        staffNumber = staffNumber.Trim().ToUpperInvariant();
        email = email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && (user.IsCandidate || await userManager.IsInRoleAsync(user, Roles.Candidate)))
            return Result.Failure<string>($"{email} belongs to a candidate account. Candidates become employees through onboarding.");
        if (await db.Users.AnyAsync(u => u.StaffNumber == staffNumber && (user == null || u.Id != user.Id), ct))
            return Result.Failure<string>($"Staff number {staffNumber} is already used by another account.");
        if (user is not null && user.StaffNumber is { Length: > 0 } own && !string.Equals(own, staffNumber, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<string>($"The account {email} already has staff number {own}.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, // registered by HR for a known person
                FullName = fullName.Trim(), PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                StaffNumber = staffNumber, DepartmentId = departmentId, SiteId = siteId, CreatedAt = DateTime.UtcNow
            };
            var created = await userManager.CreateAsync(user); // no password: they choose one with "Forgot password"
            if (!created.Succeeded) return Result.Failure<string>([.. created.Errors.Select(e => e.Description)]);
        }
        else
        {
            (user.StaffNumber, user.DepartmentId, user.SiteId) = (staffNumber, departmentId, siteId);
            if (string.IsNullOrWhiteSpace(user.PhoneNumber) && !string.IsNullOrWhiteSpace(phone)) user.PhoneNumber = phone.Trim();
            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded) return Result.Failure<string>([.. updated.Errors.Select(e => e.Description)]);
        }
        if (!await userManager.IsInRoleAsync(user, Roles.Employee))
        {
            var added = await userManager.AddToRoleAsync(user, Roles.Employee);
            if (!added.Succeeded) return Result.Failure<string>([.. added.Errors.Select(e => e.Description)]);
        }
        await tx.CommitAsync(ct);
        return Result.Success(user.Id);
    }

    public async Task<EmployeeAccountInfo?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        if (await userManager.FindByEmailAsync(email.Trim()) is not { } u) return null;
        // A candidate is recognised by the flag or by the role, whichever is set.
        return Info(u) with { IsCandidate = u.IsCandidate || await userManager.IsInRoleAsync(u, Roles.Candidate) };
    }

    public async Task<EmployeeAccountInfo?> GetAsync(string userId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) is { } u ? Info(u) : null;

    private static EmployeeAccountInfo Info(ApplicationUser u) => new(u.Id, u.FullName, u.Email, u.PhoneNumber, u.IsActive, u.IsCandidate);

    public async Task<Result> UpdatePlacementAsync(string userId, int departmentId, int siteId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("The employee's account was not found.");
        if (user.DepartmentId == departmentId && user.SiteId == siteId) return Result.Success();
        (user.DepartmentId, user.SiteId) = (departmentId, siteId);
        var updated = await userManager.UpdateAsync(user);
        return updated.Succeeded ? Result.Success() : Result.Failure([.. updated.Errors.Select(e => e.Description)]);
    }
}
