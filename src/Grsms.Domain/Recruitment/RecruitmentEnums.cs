using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Recruitment;

// The fixed lists used by recruitment requests. They are stored in MySQL as readable text
// (e.g. "HrApproval"), not numbers, so the database can be read without this file.
// [Display(Name = ...)] is the wording shown on screens.

/// <summary>Where a recruitment request is in its approval workflow (Module 2, form QF-81-11).</summary>
public enum RecruitmentRequestStatus
{
    /// <summary>Being prepared by the requester (a department staff member or the HOD); not yet sent.</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>Sent back by a reviewer ("Return for correction"); the requester can edit and resubmit it.</summary>
    [Display(Name = "Returned for correction")] Returned,

    /// <summary>Stage 1: waiting for the Head of the request's department to review it.</summary>
    [Display(Name = "Department Head review")] DepartmentHeadReview,

    /// <summary>
    /// The Recruitment Officer review stage of the first workflow (used until November 2026). No new request enters it;
    /// requests already sitting here can still be decided by a Recruitment Officer and then go on to HR approval.
    /// </summary>
    [Display(Name = "Recruitment review (earlier workflow)")] RecruitmentReview,

    /// <summary>Stage 2: waiting for the HR Manager (a budget approval document is optional).</summary>
    [Display(Name = "HR Manager review")] HrApproval,

    /// <summary>Stage 3: waiting for the Managing Director's final approval.</summary>
    [Display(Name = "MD approval")] MdApproval,

    /// <summary>Fully approved. Only now may recruitment begin: HR converts it into a vacancy.</summary>
    [Display(Name = "Approved")] Approved,

    /// <summary>Rejected permanently by a reviewer. Final.</summary>
    [Display(Name = "Rejected")] Rejected,

    /// <summary>Withdrawn by the requester. Final.</summary>
    [Display(Name = "Cancelled")] Cancelled
}

/// <summary>
/// Type of employment the new hire will have. Form QF-81-11 calls it "Engagement type" and offers only Permanent and
/// Contract (= <see cref="FixedTermContract"/>, see <see cref="RecruitmentWorkflow.FormEngagementTypes"/>); the other
/// values remain for older requests and vacancies.
/// </summary>
public enum EmploymentType
{
    [Display(Name = "Permanent")] Permanent,
    [Display(Name = "Contract")] FixedTermContract,
    [Display(Name = "Temporary")] Temporary,
    [Display(Name = "Casual")] Casual,
    [Display(Name = "Internship")] Internship,
    [Display(Name = "National Service")] NationalService
}

/// <summary>
/// Why the position needs to be filled ("Purpose" on form QF-81-11). The form offers Internal restructuring, Replacement,
/// Expansion, New position and Other (see <see cref="RecruitmentWorkflow.FormPurposes"/>); Temporary cover remains only
/// for requests raised before the form was digitised.
/// </summary>
public enum VacancyReason
{
    [Display(Name = "New position")] NewPosition,
    [Display(Name = "Replacement")] Replacement,
    [Display(Name = "Expansion")] Expansion,
    [Display(Name = "Internal restructuring")] Restructuring,
    [Display(Name = "Temporary cover (e.g. leave)")] TemporaryCover,
    [Display(Name = "Other")] Other
}

/// <summary>Staff category of the position ("Category" on form QF-81-11).</summary>
public enum StaffCategory
{
    [Display(Name = "Executive")] Executive,
    [Display(Name = "Senior")] Senior,
    [Display(Name = "Junior")] Junior
}

/// <summary>What happened at one step of the workflow (one line of the request's history).</summary>
public enum WorkflowAction
{
    [Display(Name = "Created")] Created,
    [Display(Name = "Submitted")] Submitted,
    [Display(Name = "Resubmitted")] Resubmitted,
    [Display(Name = "Approved")] Approved,
    [Display(Name = "Returned for correction")] Returned,
    [Display(Name = "Rejected")] Rejected,
    [Display(Name = "Cancelled")] Cancelled
}

/// <summary>
/// Whether the job holder needs a driving licence ("Driving licence requirement" on form QF-81-11). Optional: by default
/// none is needed. Only <see cref="Mandatory"/> becomes an applicant knockout question when the request is converted
/// into a vacancy; <see cref="Desirable"/> is listed as a nice-to-have certification.
/// </summary>
public enum DrivingLicenceRequirement
{
    [Display(Name = "Not needed")] NotNeeded,
    [Display(Name = "Desirable")] Desirable,
    [Display(Name = "Mandatory")] Mandatory
}
