using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Grsms.Web.Helpers;

/// <summary>
/// Shows enum values with their friendly wording from [Display(Name = "...")],
/// e.g. RecruitmentRequestStatus.HrApproval → "HR approval". Usage in a view: @Model.Status.DisplayName()
/// </summary>
public static class EnumExtensions
{
    public static string DisplayName(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName()
        ?? value.ToString();
}
