using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Recruitment;

// The fixed lists used by recruitment requests. They are stored in MySQL as readable text
// (e.g. "HrApproval"), not numbers, so the database can be read without this file.
// [Display(Name = ...)] is the wording shown on screens.

/// <summary>Where a recruitment request is in its approval workflow (Module 2).</summary>
public enum RecruitmentRequestStatus
{
    /// <summary>Being prepared by the Head of Department; not yet sent.</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>Sent back by a reviewer; the HOD can edit and resubmit it.</summary>
    [Display(Name = "Returned for changes")] Returned,

    /// <summary>Waiting for a Recruitment Officer to review it.</summary>
    [Display(Name = "Recruitment review")] RecruitmentReview,

    /// <summary>Waiting for the HR Manager (including budget validation).</summary>
    [Display(Name = "HR approval")] HrApproval,

    /// <summary>Waiting for the Managing Director's final approval.</summary>
    [Display(Name = "MD approval")] MdApproval,

    /// <summary>Fully approved. A vacancy can now be created from it (Phase 3).</summary>
    [Display(Name = "Approved")] Approved,

    /// <summary>Rejected permanently by a reviewer. Final.</summary>
    [Display(Name = "Rejected")] Rejected,

    /// <summary>Withdrawn by the requester. Final.</summary>
    [Display(Name = "Cancelled")] Cancelled
}

/// <summary>Type of employment the new hire will have.</summary>
public enum EmploymentType
{
    [Display(Name = "Permanent")] Permanent,
    [Display(Name = "Fixed-term contract")] FixedTermContract,
    [Display(Name = "Temporary")] Temporary,
    [Display(Name = "Casual")] Casual,
    [Display(Name = "Internship")] Internship,
    [Display(Name = "National Service")] NationalService
}

/// <summary>Why the position needs to be filled.</summary>
public enum VacancyReason
{
    [Display(Name = "New position")] NewPosition,
    [Display(Name = "Replacement (resignation, retirement, termination)")] Replacement,
    [Display(Name = "Business expansion")] Expansion,
    [Display(Name = "Restructuring")] Restructuring,
    [Display(Name = "Temporary cover (e.g. leave)")] TemporaryCover
}

/// <summary>What happened at one step of the workflow (one line of the request's history).</summary>
public enum WorkflowAction
{
    [Display(Name = "Created")] Created,
    [Display(Name = "Submitted")] Submitted,
    [Display(Name = "Resubmitted")] Resubmitted,
    [Display(Name = "Approved")] Approved,
    [Display(Name = "Returned for changes")] Returned,
    [Display(Name = "Rejected")] Rejected,
    [Display(Name = "Cancelled")] Cancelled
}
