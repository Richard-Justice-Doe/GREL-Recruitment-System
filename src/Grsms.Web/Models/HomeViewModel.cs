using Grsms.Application.Dashboard;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Models;

/// <summary>The staff home page (dashboard).</summary>
public class HomeViewModel
{
    public required DashboardDto Dashboard { get; init; }

    /// <summary>The Division / Department filter currently applied.</summary>
    public DashboardFilter Filter { get; init; } = new(null, null);

    /// <summary>Drop-downs for the filter bar (null when the user only sees their own departments).</summary>
    public SelectList? DivisionList { get; init; }
    public SelectList? DepartmentList { get; init; }

    /// <summary>Phase 21: the onboarding and probation widgets (HR and Head of Department).</summary>
    public Application.Workforce.WorkforcePanelDto Workforce { get; init; } = new([], []);
}
