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
    public required IReadOnlyList<KpiCardDto> Kpis { get; init; }

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
}

/// <summary>Which list a KPI card links to.</summary>
public enum KpiKind { Requests, Approved, OpenVacancies, Applications, Candidates }

/// <summary>
/// One KPI card: a total, a bar per day for the last 7 days (oldest first),
/// and the 7-day total compared with the 7 days before.
/// </summary>
public record KpiCardDto(KpiKind Kind, string Title, string Icon, int Total, IReadOnlyList<DayCount> Last7Days, int PreviousWeek)
{
    public int ThisWeek => Last7Days.Sum(d => d.Count);

    /// <summary>Change against the previous 7 days in percent; null when there is nothing to compare with.</summary>
    public int? ChangePercent => PreviousWeek == 0 ? null : (int)Math.Round((ThisWeek - PreviousWeek) * 100.0 / PreviousWeek);
}

public record DayCount(DateOnly Day, int Count);

/// <summary>What a dashboard task opens.</summary>
public enum DashboardTaskTarget { Request, Vacancy, NewVacancyForRequest, Shortlist, Interview, Hiring, Onboarding, NewOnboarding, InterviewValidation, HiringRecommendation }

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
