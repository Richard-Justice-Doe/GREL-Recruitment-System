using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Hiring;

// Module 8: background checks, the hiring recommendation (hiring report) with its approvals, and offers.

public enum CheckType
{
    [Display(Name = "References")] References,
    [Display(Name = "Education / certificates verification")] Education,
    [Display(Name = "Previous employment")] Employment,
    [Display(Name = "Criminal record (police report)")] CriminalRecord,
    [Display(Name = "Medical fitness")] Medical,
    [Display(Name = "Other")] Other
}

public enum CheckStatus
{
    [Display(Name = "Pending")] Pending,
    [Display(Name = "Cleared")] Cleared,
    [Display(Name = "Cleared with concerns")] Concerns,
    [Display(Name = "Failed")] Failed
}

/// <summary>One background check of an applicant (e.g. a reference from their last employer).</summary>
public class BackgroundCheck : BaseEntity
{
    public int JobApplicationId { get; set; }
    public CheckType Type { get; set; }

    /// <summary>Who was contacted or what was checked, e.g. "Mr K. Owusu, Plant Manager, Tema Rubber Ltd".</summary>
    public string Subject { get; set; } = string.Empty;

    public CheckStatus Status { get; set; } = CheckStatus.Pending;
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
}

public enum HiringStatus
{
    [Display(Name = "Waiting for the Head of Department")] AwaitingHod,
    [Display(Name = "Waiting for the HR Manager")] AwaitingHr,
    [Display(Name = "Waiting for the Managing Director")] AwaitingMd,
    [Display(Name = "Approved")] Approved,
    [Display(Name = "Rejected")] Rejected,
    [Display(Name = "Withdrawn")] Withdrawn
}

public enum HiringDecision
{
    [Display(Name = "Not yet")] None,
    [Display(Name = "Endorse / approve")] Approve,
    [Display(Name = "Do not endorse / reject")] Reject
}

/// <summary>
/// The final hiring recommendation for one applicant: the "hiring report". The recruiter proposes the hire (salary,
/// start date, justification), the department's Head of Department endorses or not (GREL requirement: HODs take part in
/// the final hiring recommendation), then the HR Manager and the Managing Director approve.
/// Stores the division, department and requesting HOD of its vacancy, like every recruitment record.
/// </summary>
public class HiringRecommendation : BaseEntity
{
    /// <summary>Reference, e.g. "HIR-2026-0001". Unique.</summary>
    public string Number { get; set; } = string.Empty;

    public int JobApplicationId { get; set; }
    public int VacancyId { get; set; }
    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    /// <summary>Monthly salary proposed, in GHS.</summary>
    public decimal ProposedSalary { get; set; }
    public DateOnly ProposedStartDate { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;

    public HiringStatus Status { get; set; } = HiringStatus.AwaitingHod;

    public HiringDecision HodDecision { get; set; }
    public string? HodComment { get; set; }
    public string? HodUserId { get; set; }
    public DateTime? HodDecidedAt { get; set; }

    public HiringDecision HrDecision { get; set; }
    public string? HrComment { get; set; }
    public string? HrUserId { get; set; }
    public DateTime? HrDecidedAt { get; set; }

    public HiringDecision MdDecision { get; set; }
    public string? MdComment { get; set; }
    public string? MdUserId { get; set; }
    public DateTime? MdDecidedAt { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();
}

public enum OfferStatus
{
    [Display(Name = "Sent, waiting for the candidate")] Sent,
    [Display(Name = "Accepted")] Accepted,
    [Display(Name = "Declined")] Declined,
    [Display(Name = "Withdrawn")] Withdrawn,
    [Display(Name = "Expired")] Expired
}

/// <summary>An offer of employment to one applicant, issued after the hiring recommendation is fully approved.</summary>
public class Offer : BaseEntity
{
    /// <summary>Reference, e.g. "OFF-2026-0001". Unique.</summary>
    public string Number { get; set; } = string.Empty;

    public int JobApplicationId { get; set; }
    public int HiringRecommendationId { get; set; }
    public int VacancyId { get; set; }
    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }
    public DateOnly StartDate { get; set; }

    /// <summary>Last day the candidate can accept (inclusive).</summary>
    public DateOnly ExpiresOn { get; set; }

    /// <summary>Extra terms shown in the letter (probation, benefits, ...).</summary>
    public string? Terms { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Sent;
    public DateTime SentAt { get; set; }
    public string SentByUserId { get; set; } = string.Empty;
    public DateTime? RespondedAt { get; set; }

    /// <summary>The candidate's reason for declining, or the reason for withdrawing.</summary>
    public string? Reason { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();
}
