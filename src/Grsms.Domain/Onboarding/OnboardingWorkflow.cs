using Grsms.Domain.Security;

namespace Grsms.Domain.Onboarding;

/// <summary>
/// The rules of Module 9, in one place:
///
///   Start      : a Recruitment Officer or HR Manager starts onboarding once the candidate has ACCEPTED the offer
///                (one onboarding per application). The default checklist below is created, with owners.
///   Letter     : an HR Manager issues the appointment letter (and may revise it until it is signed). The new
///                employee reads it in their portal and signs it by typing their full name. Signed letters are locked.
///   Checklist  : each task is done by its owner, by the department's HOD (department tasks), or by HR (any task).
///                The new employee's items ("bring on your first day") are ticked off by HR when received.
///   Complete   : HR completes the onboarding once the letter is signed and no task is still "To do".
///   Cancel     : HR cancels with a reason (e.g. the new employee did not report).
/// </summary>
public static class OnboardingWorkflow
{
    /// <summary>Who runs onboarding: the same people who run recruitment.</summary>
    public static readonly IReadOnlyList<string> HrRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Department code of the IT department, whose Head receives the IT tasks by default.</summary>
    public const string ItDepartmentCode = "IT";

    /// <summary>One item of the default checklist. <paramref name="DueDays"/> is counted from the start date.</summary>
    public record DefaultTask(OnboardingTeam Team, string Title, int DueDays);

    /// <summary>The checklist every onboarding starts with. HR can add items, reassign them or mark them "not needed".</summary>
    public static readonly IReadOnlyList<DefaultTask> DefaultChecklist =
    [
        // Human Resources
        new(OnboardingTeam.HumanResources, "Open the personal file and verify the original certificates", -3),
        new(OnboardingTeam.HumanResources, "Schedule the GREL induction programme", -5),
        new(OnboardingTeam.HumanResources, "Payroll set-up: bank account, SSNIT and GRA tax details, staff number", 0),
        new(OnboardingTeam.HumanResources, "Enrol in benefits (medical cover, provident fund)", 14),
        new(OnboardingTeam.HumanResources, "Change the GRSMS account to a staff account (Users & roles)", 1),

        // Department
        new(OnboardingTeam.Department, "Workspace, tools and PPE ready", -1),
        new(OnboardingTeam.Department, "Department induction and introduction to the team", 0),
        new(OnboardingTeam.Department, "Agree probation objectives", 14),

        // IT
        new(OnboardingTeam.InformationTechnology, "Create the email account and system access", -2),
        new(OnboardingTeam.InformationTechnology, "Issue a computer / phone (mark \"not needed\" if none)", -1),

        // The new employee (shown in their portal as "bring on your first day")
        new(OnboardingTeam.NewEmployee, "Ghana Card (original and a copy)", 0),
        new(OnboardingTeam.NewEmployee, "SSNIT number", 0),
        new(OnboardingTeam.NewEmployee, "Bank account details", 0),
        new(OnboardingTeam.NewEmployee, "Original certificates", 0),
        new(OnboardingTeam.NewEmployee, "Two passport photographs", 0)
    ];

    public static bool IsFinished(OnboardingTaskStatus s) => s != OnboardingTaskStatus.Open;
}
