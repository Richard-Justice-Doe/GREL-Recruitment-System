using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Applications;

// Fixed lists used by candidate profiles and job applications (Module 5). Stored in MySQL as readable text.

/// <summary>
/// Where an application stands. Phase 5 uses Submitted, Under review, Not successful and Withdrawn; the later stages
/// are set by the shortlisting (6), interview (7), offer (8) and onboarding (9) modules.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Received; nobody has started reviewing it yet.</summary>
    [Display(Name = "Received")] Submitted,

    [Display(Name = "Under review")] UnderReview,
    [Display(Name = "Shortlisted")] Shortlisted,
    [Display(Name = "Interview")] Interview,
    [Display(Name = "Offer")] Offer,
    [Display(Name = "Hired")] Hired,

    /// <summary>Not taken further. The applicant is told politely; the reason stays internal.</summary>
    [Display(Name = "Not successful")] NotSuccessful,

    /// <summary>The applicant withdrew it.</summary>
    [Display(Name = "Withdrawn")] Withdrawn,

    /// <summary>The applicant declined the offer (Phase 8).</summary>
    [Display(Name = "Offer declined")] OfferDeclined
}

/// <summary>Where the applicant saw the job (for advert effectiveness reports).</summary>
public enum ApplicationSource
{
    [Display(Name = "GREL careers page")] CareersPage,
    [Display(Name = "GRSMS internal portal")] InternalPortal,
    [Display(Name = "LinkedIn")] LinkedIn,
    [Display(Name = "Indeed")] Indeed,
    [Display(Name = "Jobberman")] Jobberman,
    [Display(Name = "Newspaper")] Newspaper,
    [Display(Name = "Referred by someone")] Referral,
    [Display(Name = "Other")] Other
}

/// <summary>One line of an application's history.</summary>
public enum ApplicationAction
{
    [Display(Name = "Application submitted")] Submitted,
    [Display(Name = "Review started")] ReviewStarted,
    [Display(Name = "Marked not successful")] NotSuccessful,
    [Display(Name = "Withdrawn by the applicant")] Withdrawn,

    // Phase 6: shortlisting
    [Display(Name = "Recruiter recommendation")] Recommended,
    [Display(Name = "Head of Department's view")] HodReviewed,
    [Display(Name = "Shortlisted")] Shortlisted,
    [Display(Name = "Shortlisted (HR Manager override)")] ShortlistedOverride,

    // Phase 7: interviews and assessments
    [Display(Name = "Interview / assessment scheduled")] InterviewScheduled,
    [Display(Name = "Interview / assessment completed")] InterviewCompleted,
    [Display(Name = "Interview / assessment cancelled")] InterviewCancelled,
    [Display(Name = "Did not attend")] InterviewNoShow,

    // Phase 8: background checks, hiring recommendation and offer
    [Display(Name = "Background check recorded")] CheckRecorded,
    [Display(Name = "Hiring recommendation")] HiringRecommended,
    [Display(Name = "Hiring decision")] HiringDecided,
    [Display(Name = "Offer sent")] OfferSent,
    [Display(Name = "Offer accepted")] OfferAccepted,
    [Display(Name = "Offer declined")] OfferDeclined,
    [Display(Name = "Offer withdrawn")] OfferWithdrawn,
    [Display(Name = "Offer expired")] OfferExpired,

    // Phase 9: appointment letter and onboarding
    [Display(Name = "Onboarding started")] OnboardingStarted,
    [Display(Name = "Appointment letter issued")] LetterIssued,
    [Display(Name = "Appointment letter signed")] LetterSigned,
    [Display(Name = "Onboarding completed")] OnboardingCompleted,
    [Display(Name = "Onboarding cancelled")] OnboardingCancelled
}

/// <summary>A recommendation on whether to shortlist an application (Phase 6).</summary>
public enum ShortlistRecommendation
{
    [Display(Name = "Not given")] None,
    [Display(Name = "Shortlist")] Shortlist,
    [Display(Name = "Do not shortlist")] DoNotShortlist
}
