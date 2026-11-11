using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Advertisements;

// Fixed lists used by job adverts (Module 4). Stored in MySQL as readable text.

/// <summary>Life cycle of a job advert.</summary>
public enum AdvertStatus
{
    /// <summary>Being prepared by the Recruitment Officer. Not visible anywhere.</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>11 Nov 2026 (docs/29 change 2): sent in by the Recruitment Officer; the HR Manager checks it.</summary>
    [Display(Name = "Awaiting HR review")] AwaitingHrReview,

    /// <summary>Reviewed by the HR Manager; the IT Manager gives the final approval, which publishes it.</summary>
    [Display(Name = "Awaiting IT approval")] AwaitingItApproval,

    /// <summary>Sent back by the HR or IT Manager (or taken offline) with the changes needed; the Recruitment Officer corrects and resubmits it.</summary>
    [Display(Name = "Returned for correction")] ReturnedForCorrection,

    /// <summary>Approved and published, waiting for its start date. Goes live automatically on that day.</summary>
    [Display(Name = "Scheduled")] Scheduled,

    /// <summary>Published and running: shown on the internal portal and/or careers website, and ready to post on the other channels.</summary>
    [Display(Name = "Published")] Live,

    /// <summary>Finished (end date passed, vacancy closed, or closed by hand). Kept for history; can be cloned.</summary>
    [Display(Name = "Closed")] Closed,

    /// <summary>Rejected by the HR or IT Manager. Final; kept for history and can be cloned into a new draft.</summary>
    [Display(Name = "Rejected")] Rejected
}

/// <summary>Where an advert appears.</summary>
public enum AdvertChannel
{
    /// <summary>The staff-only jobs page inside GRSMS. Published automatically.</summary>
    [Display(Name = "Internal portal")] InternalPortal,

    /// <summary>The public careers page of GRSMS (linked from GREL's website). Published automatically.</summary>
    [Display(Name = "Company website")] CompanyWebsite,

    [Display(Name = "LinkedIn")] LinkedIn,
    [Display(Name = "Indeed")] Indeed,
    [Display(Name = "Jobberman")] Jobberman,
    [Display(Name = "Newspaper")] Newspaper
}

/// <summary>Where one channel of an advert stands.</summary>
public enum ChannelPostStatus
{
    /// <summary>Not yet placed (external channels wait here until the recruiter posts the copy-ready text).</summary>
    [Display(Name = "To post")] ToPost,

    /// <summary>Placed on the channel.</summary>
    [Display(Name = "Posted")] Posted,

    /// <summary>Taken down again.</summary>
    [Display(Name = "Removed")] Removed
}

/// <summary>One line of an advert's history.</summary>
public enum AdvertAction
{
    [Display(Name = "Created")] Created,
    [Display(Name = "Cloned")] Cloned,
    [Display(Name = "Scheduled")] Scheduled,
    [Display(Name = "Schedule cancelled")] Unscheduled,
    [Display(Name = "Published")] Published,
    [Display(Name = "Published automatically (start date reached)")] AutoPublished,
    [Display(Name = "Posted on a channel")] ChannelPosted,
    [Display(Name = "Removed from a channel")] ChannelRemoved,
    [Display(Name = "Closed")] Closed,
    [Display(Name = "Closed automatically")] AutoClosed,

    // 11 Nov 2026 (docs/29 change 2): HR review, then IT approval.
    [Display(Name = "Sent for HR review")] Submitted,
    [Display(Name = "Reviewed by HR, sent for IT approval")] HrApproved,
    [Display(Name = "Approved by IT")] ItApproved,
    [Display(Name = "Returned for correction")] Returned,
    [Display(Name = "Rejected")] Rejected,
    [Display(Name = "Taken offline")] Unpublished
}
