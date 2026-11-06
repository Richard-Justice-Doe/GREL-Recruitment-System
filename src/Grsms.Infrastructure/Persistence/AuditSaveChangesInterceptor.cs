using System.Text.Json;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Auditing;
using Grsms.Domain.Common;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// Runs automatically every time data is saved. It does three jobs so no screen or service can forget them:
///
///  1. AUDIT FIELDS - fills CreatedAt/CreatedBy on new rows and UpdatedAt/UpdatedBy on changed rows.
///  2. SOFT DELETE  - turns a DELETE into an UPDATE that sets IsDeleted = true (history is never lost).
///  3. AUDIT TRAIL  - writes one AuditLogs row per changed record, with the old and new values as JSON,
///                    the user and their IP address (Module 22).
///
/// The record changes and their audit rows are saved in the SAME database transaction, so you can never
/// end up with a change that has no audit row (or an audit row for a change that was rolled back).
///
/// Registered as "scoped" (one instance per web request); see Infrastructure/DependencyInjection.cs.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    /// <summary>
    /// Fields never copied into the audit trail: secrets (password hash, security stamp) and noisy
    /// bookkeeping that changes on every sign-in.
    /// </summary>
    private static readonly HashSet<string> ExcludedProperties =
    [
        nameof(IdentityUser.PasswordHash),
        nameof(IdentityUser.SecurityStamp),
        nameof(IdentityUser.ConcurrencyStamp),
        nameof(IdentityUser.AccessFailedCount),
        nameof(ApplicationUser.LastSignInAt),
        nameof(IdentityUserToken<string>.Value),
        nameof(BaseEntity.CreatedAt),
        nameof(BaseEntity.CreatedBy),
        nameof(BaseEntity.UpdatedAt),
        nameof(BaseEntity.UpdatedBy),
        nameof(BaseEntity.DeletedAt),
        nameof(BaseEntity.DeletedBy),
        // Phase 15: the full text of a CV (it is in the CV file itself; the audit trail records what the ATS decided).
        nameof(Domain.Applications.JobApplication.CvText),
    ];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    // State carried from "before save" to "after save" of one SaveChanges call.
    private List<PendingAudit> _pending = [];
    private IDbContextTransaction? _ownTransaction;
    private bool _savingAuditRows;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is { } db && !_savingAuditRows)
        {
            Prepare(db);
            // Start our own transaction only if the caller has not already started one.
            if (_pending.Count > 0 && db.Database.CurrentTransaction is null)
                _ownTransaction = await db.Database.BeginTransactionAsync(ct);
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (eventData.Context is { } db && !_savingAuditRows && _pending.Count > 0)
        {
            // Now that the database has generated new ids, the audit rows can record them.
            db.Set<AuditLog>().AddRange(_pending.Select(p => p.ToAuditLog()));
            _pending = [];

            _savingAuditRows = true; // stop this interceptor reacting to its own save
            try { await db.SaveChangesAsync(ct); }
            finally { _savingAuditRows = false; }

            if (_ownTransaction is not null)
            {
                await _ownTransaction.CommitAsync(ct);
                await _ownTransaction.DisposeAsync();
                _ownTransaction = null;
            }
        }
        return result;
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
    {
        if (_savingAuditRows) return; // the outer handler below will clean up
        _pending = [];
        if (_ownTransaction is not null)
        {
            await _ownTransaction.RollbackAsync(ct);
            await _ownTransaction.DisposeAsync();
            _ownTransaction = null;
        }
    }

    // The synchronous SaveChanges() path does the same work, for any code that does not use async.

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } db && !_savingAuditRows)
        {
            Prepare(db);
            if (_pending.Count > 0 && db.Database.CurrentTransaction is null)
                _ownTransaction = db.Database.BeginTransaction();
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } db && !_savingAuditRows && _pending.Count > 0)
        {
            db.Set<AuditLog>().AddRange(_pending.Select(p => p.ToAuditLog()));
            _pending = [];
            _savingAuditRows = true;
            try { db.SaveChanges(); }
            finally { _savingAuditRows = false; }
            _ownTransaction?.Commit();
            _ownTransaction?.Dispose();
            _ownTransaction = null;
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (_savingAuditRows) return;
        _pending = [];
        _ownTransaction?.Rollback();
        _ownTransaction?.Dispose();
        _ownTransaction = null;
    }

    /// <summary>Applies jobs 1 and 2 and captures what job 3 needs, BEFORE the data is written.</summary>
    private void Prepare(DbContext db)
    {
        var now = DateTime.UtcNow;
        // Work the system does on its own inside someone's request (e.g. ATS screening) is recorded as the system.
        var user = Application.Common.SystemActor.Current ?? currentUser.UserName ?? "system";
        _pending = [];

        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            // Audit rows themselves, and the technical reference-number counters, are not audited.
            if (entry.Entity is AuditLog or NumberSequence or Domain.Security.KnownPermission) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            // Phase 19: a signed Personal Record Declaration is immutable. Nothing may change or remove it, not even soft-delete.
            if (entry.Entity is Domain.Onboarding.EmployeeDeclaration && entry.State != EntityState.Added)
                throw new InvalidOperationException("A signed declaration can never be changed or deleted.");

            string action;
            if (entry.Entity is BaseEntity entity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entity.CreatedAt = now;
                        entity.CreatedBy = user;
                        action = AuditActions.Created;
                        break;

                    case EntityState.Deleted:
                        // Job 2: soft delete. Keep the row, just flag it.
                        entry.State = EntityState.Modified;
                        entity.IsDeleted = true;
                        entity.DeletedAt = now;
                        entity.DeletedBy = user;
                        action = AuditActions.Deleted;
                        break;

                    default:
                        entity.UpdatedAt = now;
                        entity.UpdatedBy = user;
                        action = AuditActions.Updated;
                        break;
                }
            }
            else
            {
                // Identity tables (users, role assignments, ...) are really inserted/deleted,
                // but are still audited.
                action = entry.State switch
                {
                    EntityState.Added => AuditActions.Created,
                    EntityState.Deleted => AuditActions.Deleted,
                    _ => AuditActions.Updated
                };
            }

            // Notifications, reminder keys and confidential survey answers are not copied into the audit trail.
            if (entry.Entity is INotAudited) continue;

            var audit = Capture(entry, action);
            if (audit is not null) _pending.Add(audit);
        }
    }

    /// <summary>Records the before/after values of one changed row. Returns null if nothing worth auditing changed.</summary>
    private PendingAudit? Capture(EntityEntry entry, string action)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (ExcludedProperties.Contains(name)) continue;

            switch (action)
            {
                case AuditActions.Created:
                    if (!p.IsTemporary && p.CurrentValue is not null) newValues[name] = p.CurrentValue;
                    break;

                case AuditActions.Deleted:
                    if (name != nameof(BaseEntity.IsDeleted)) oldValues[name] = p.OriginalValue;
                    break;

                default: // Updated: only the fields whose value really changed
                    if (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                    {
                        oldValues[name] = p.OriginalValue;
                        newValues[name] = p.CurrentValue;
                    }
                    break;
            }
        }

        // e.g. a sign-in that only bumped LastSignInAt: nothing meaningful changed.
        if (action == AuditActions.Updated && newValues.Count == 0) return null;

        // Phase 16: a secret setting (SMTP password, Microsoft 365 secret) shows only that it changed, never a value.
        if (entry.Entity is Domain.Settings.SystemSetting { IsSecret: true })
        {
            const string field = nameof(Domain.Settings.SystemSetting.SettingValue);
            if (oldValues.ContainsKey(field)) oldValues[field] = oldValues[field] is null ? null : "(secret)";
            if (newValues.ContainsKey(field)) newValues[field] = newValues[field] is null ? null : "(secret, changed)";
        }

        return new PendingAudit(
            entry,
            EntityNameOf(entry),
            action,
            oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, Json),
            newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, Json),
            Application.Common.SystemActor.Current is null ? currentUser.UserId : null,
            Application.Common.SystemActor.Current ?? currentUser.UserName,
            Application.Common.SystemActor.Current is null ? currentUser.IpAddress : null,
            DateTime.UtcNow);
    }

    /// <summary>A friendly record-type name for the audit trail ("Department", "User", "UserRole", ...).</summary>
    private static string EntityNameOf(EntityEntry entry) => entry.Entity switch
    {
        ApplicationUser => "User",
        IdentityUserRole<string> => "UserRole",
        IdentityUserLogin<string> => "UserLogin",
        IdentityUserClaim<string> => "UserClaim",
        IdentityUserToken<string> => "UserToken",
        IdentityRole => "Role",
        IdentityRoleClaim<string> => "RoleClaim", // Phase 14: a role's permissions (and a custom role's description)
        _ => entry.Metadata.ClrType.Name
    };

    /// <summary>An audit row waiting for the main save to finish (so new record ids are known).</summary>
    private sealed record PendingAudit(
        EntityEntry Entry, string EntityName, string Action, string? OldValues, string? NewValues,
        string? UserId, string? UserName, string? IpAddress, DateTime Timestamp)
    {
        public AuditLog ToAuditLog() => new()
        {
            Timestamp = Timestamp,
            UserId = UserId,
            UserName = UserName,
            Action = Action,
            EntityName = EntityName,
            // Read the key AFTER saving, so auto-generated ids are filled in. Composite keys are joined with ",".
            EntityId = string.Join(",", Entry.Metadata.FindPrimaryKey()!.Properties
                .Select(k => Entry.Property(k.Name).CurrentValue?.ToString())),
            OldValues = OldValues,
            NewValues = NewValues,
            IpAddress = IpAddress
        };
    }
}
