using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Celebrations;

/// <summary>17 November 2026: what is being celebrated.</summary>
public enum CelebrationKind
{
    [Display(Name = "Birthday")] Birthday,
    [Display(Name = "Work anniversary")] Anniversary
}

/// <summary>
/// A colleague's message under a celebration ("Happy birthday!"). Kept per employee, kind and year, so last year's messages
/// don't show this year. HR can hide a message (moderation); hidden messages stay in the record with who hid them.
/// </summary>
public class CelebrationMessage : BaseEntity
{
    public int EmployeeId { get; set; }
    public CelebrationKind Kind { get; set; }
    public int Year { get; set; }

    [MaxLength(450)] public string AuthorUserId { get; set; } = string.Empty;
    [MaxLength(256)] public string AuthorName { get; set; } = string.Empty;
    [MaxLength(280)] public string Text { get; set; } = string.Empty;

    public bool IsHidden { get; set; }
    [MaxLength(256)] public string? HiddenBy { get; set; }
}
