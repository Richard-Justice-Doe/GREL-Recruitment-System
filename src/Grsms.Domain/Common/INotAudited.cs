namespace Grsms.Domain.Common;

/// <summary>
/// Marks records that are NOT copied into the audit trail: high-volume bookkeeping (notifications, reminder keys)
/// and confidential survey answers. Their own tables keep who and when.
/// </summary>
public interface INotAudited;
