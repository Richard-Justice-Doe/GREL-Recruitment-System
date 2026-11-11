namespace Grsms.Web.Helpers;

/// <summary>
/// docs/32 (11 November 2026): where each staff page sits in the menu: its group (Recruitment, Candidates, Hiring, Employees,
/// Reports) and its menu item with that item's list. Used by the sidebar (which group is open) and by the breadcrumb at the
/// top of each page ("Recruitment › Vacancies › VAC-2026-0012"). Administration screens have their own list in
/// AdministrationController. Pages not listed here (Home, the candidate portal, profile, ...) have no breadcrumb.
/// </summary>
public static class MenuMap
{
    /// <param name="Group">Menu group, e.g. "Recruitment".</param>
    /// <param name="Title">Menu item (or tab), e.g. "Vacancies".</param>
    /// <param name="Controller">Its list page's controller.</param>
    /// <param name="ListAction">Its list page's action; any other action of the controller is a page inside it.</param>
    public record Place(string Group, string Title, string Controller, string ListAction = "Index");

    public static Place? For(string? controller, string? action) => controller switch
    {
        "RecruitmentRequests" => new("Recruitment", "Recruitment requests", controller),
        "Vacancies" => new("Recruitment", "Vacancies", controller),
        "Adverts" => new("Recruitment", "Job adverts", controller),
        "Applications" => new("Candidates", "Applications", controller),
        "Ats" when action != "Settings" => new("Candidates", "Screening", controller),
        "Shortlisting" => new("Candidates", "Candidates", "Applications"),
        "Interviews" => new("Candidates", "Interviews", controller),
        "Hiring" => new("Hiring", "Hiring & offers", controller),
        "Onboarding" => new("Hiring", "Onboarding", controller),
        "Employees" => new("Employees", "Employees", controller),
        "Probation" => new("Employees", "Probation", controller),
        "PersonnelFiles" => new("Employees", "Personnel files", controller),
        "Reports" => new("Reports", "Reports", controller),
        "Surveys" when action == "Results" => new("Reports", "Reports", "Reports"),
        _ => null
    };
}
