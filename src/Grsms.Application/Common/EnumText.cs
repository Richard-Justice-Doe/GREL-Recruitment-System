using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Grsms.Application.Common;

/// <summary>
/// The friendly wording of an enum value from its [Display(Name = "...")], for text the services write
/// (advert text, emails). Views use the equivalent DisplayName() extension in the web project.
/// </summary>
public static class EnumText
{
    public static string Label(Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName()
        ?? value.ToString();
}
