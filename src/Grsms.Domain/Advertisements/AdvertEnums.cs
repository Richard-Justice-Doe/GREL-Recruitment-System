using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Advertisements;

// Fixed lists used by job adverts (Module 4). Stored in MySQL as readable text.

/// <summary>Life cycle of a job advert.</summary>
public enum AdvertStatus
{
    /// <summary>Being prepared. Not visible anywhere.</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>Ready, waiting for its start date. Goes live automatically on that day.</summary>
    [Display(Name = "Scheduled")] Scheduled,

    /// <summary>Running: shown on the internal portal and/or careers website, and ready to post on the other channels.</summary>
    [Display(Name = "Live")] Live,

    /// <summary>Finished (end date passed, vacancy closed, or closed by hand). Kept for history; can be cloned.</summary>
    [Display(Name = "Closed")] Closed
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
    [Display(Name = "Closed automatically")] AutoClosed
}
