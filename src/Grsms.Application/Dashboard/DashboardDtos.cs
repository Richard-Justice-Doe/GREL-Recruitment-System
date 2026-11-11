using Grsms.Application.Users;

namespace Grsms.Application.Dashboard;

/// <summary>What the signed-in user may see on the dashboard. Decided by the web layer from its authorization policies.</summary>
public record DashboardAccess(bool Requests, bool Vacancies, bool Candidates);

/// <summary>Optional Division / Department filter chosen at the top of the dashboard.</summary>
public record DashboardFilter(int? DivisionId, int? DepartmentId)
{
    public bool IsSet => DivisionId is not null || DepartmentId is not null;
}

/// <summary>Everything on the staff home page (dashboard).</summary>
public record DashboardDto
{
    /// <summary>docs/32 (11 November 2026): the action cards at the top (they replace the KPI row with 7-day charts).</summary>
    public required IReadOnlyList<ActionCardDto> Cards { get; init; }

    /// <summary>The oldest few items waiting for the current user, and how many there are in all.</summary>
    public required IReadOnlyList<DashboardTaskDto> Tasks { get; init; }
    public int TaskTotal { get; init; }

    /// <summary>Vacancy closing dates for <see cref="Month"/>; null when the user cannot see vacancies.</summary>
    public IReadOnlyList<ClosingDayDto>? ClosingDays { get; init; }
    public DateOnly Month { get; init; }

    /// <summary>Requests moving through the approval stages; null when the user cannot see requests.</summary>
    public IReadOnlyList<RequestProgressDto>? InProgress { get; init; }
    public int InProgressTotal { get; init; }

    /// <summary>Newest candidate accounts; null when the user may not see candidates.</summary>
    public IReadOnlyList<CandidateSummary>? LatestCandidates { get; init; }

    /// <summary>The IT Manager's job advert cards (awaiting approval, scheduled, active, expired); null for everyone else.</summary>
    public Grsms.Application.Adverts.AdvertItSummary? AdvertCards { get; init; }
}

/// <summary>
/// docs/32 (11 November 2026): one action card on the home page: how many things need attention, and the list that shows them.
/// </summary>
/// <param name="Title">e.g. "Requests awaiting your approval".</param>
/// <param name="Count">The number on the card.</param>
/// <param name="Hint">One line under the number, e.g. "Submitted in the last 7 days".</param>
/// <param name="Tone">Colour family (tone-hire, tone-interview, ...).</param>
/// <param name="Controller">The list it opens.</param>
/// <param name="Action">The list's action.</param>
/// <param name="Route">The list's tab and filters.</param>
/// <param name="Attention">True when the items are waiting for the user (the card is highlighted while the count is above 0).</param>
public record ActionCardDto(string Title, string Icon, int Count, string Hint, string Tone, string Controller, string Action,
    IReadOnlyDictionary<string, string> Route, bool Attention);

/// <summary>What a dashboard task opens.</summary>
public enum DashboardTaskTarget
{
    Request, Vacancy, NewVacancyForRequest, Shortlist, Interview, Hiring, Onboarding, NewOnboarding, InterviewValidation, HiringRecommendation,

    // Usability U1 (10 Nov 2026): the rest of the work that waits for someone.
    AtsConfirm, OnboardingLetter, OnboardingDocuments, OnboardingActivate, OnboardingInduction, ProbationStep, ProbationDecision,

    // 11 Nov 2026 (docs/29 change 2): a job advert to review, approve or correct.
    Advert
}

/// <summary>One row of "My tasks". <see cref="Status"/> is the record's workflow status enum (shown with its display name).</summary>
public record DashboardTaskDto(
    DashboardTaskTarget Target,
    int Id,
    string Title,
    string Reference,
    string Action,
    string PersonName,
    Enum Status,
    DateTime Since);

/// <summary>One day in the vacancy closing calendar.</summary>
public record ClosingDayDto(DateOnly Date, IReadOnlyList<ClosingVacancyDto> Vacancies);

public record ClosingVacancyDto(int Id, string VacancyNumber, string JobTitle, string DepartmentName, int Positions, bool IsClosed);

/// <summary>A request in the approval workflow, with how far along it is.</summary>
public record RequestProgressDto(int Id, string RequestNumber, string PositionTitle, string RequestedByName, Enum Status, int Percent);
