namespace Grsms.Domain.Common;

/// <summary>
/// The parent class for every business record in GRSMS (departments, vacancies, candidates, ...).
///
/// It gives each table the same standard columns, so nobody has to remember to add them:
///   * <see cref="Id"/>            - the primary key.
///   * Created/Updated fields      - who changed the record and when (filled in automatically
///                                   by the database layer; see AuditSaveChangesInterceptor).
///   * Soft-delete fields          - records are never physically removed; they are marked as
///                                   deleted and hidden from normal screens, so history is kept.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Primary key. MySQL generates it (AUTO_INCREMENT).</summary>
    public int Id { get; set; }

    /// <summary>When the record was first saved (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>User name of the person who created the record ("system" for seed data).</summary>
    public string? CreatedBy { get; set; }

    /// <summary>When the record was last changed (UTC). Empty until the first edit.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User name of the person who last changed the record.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>True once the record has been "deleted". Deleted rows are hidden by a global query filter.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the record was deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>User name of the person who deleted the record.</summary>
    public string? DeletedBy { get; set; }
}
