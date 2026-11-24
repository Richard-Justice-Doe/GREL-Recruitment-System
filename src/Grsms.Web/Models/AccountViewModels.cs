using System.ComponentModel.DataAnnotations;

namespace Grsms.Web.Models;

// Form models for the sign-in pages. The attributes give both browser-side and server-side validation.

/// <summary>Sign-in form.</summary>
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// "Keep me signed in on this device". Even when ticked the session still ends after the idle
    /// timeout; it only survives closing the browser.
    /// </summary>
    [Display(Name = "Keep me signed in on this device")]
    public bool RememberMe { get; set; }

    /// <summary>Page to return to after signing in (only local addresses are honoured).</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Candidate self-registration form (public candidate portal).</summary>
public class RegisterViewModel
{
    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>"Forgot password" and "Resend confirmation email" forms: just an email address.</summary>
public class EmailOnlyViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

/// <summary>The form reached from the password-reset email.</summary>
public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>The one-time code from the email link.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Change-password form for a signed-in user.</summary>
public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>True when the user was forced here because an administrator set a temporary password.</summary>
    public bool IsForced { get; set; }
}

/// <summary>22 Nov 2026 (docs/41, H2): the second step of signing in: the code from the authenticator app, or a recovery code.</summary>
public class LoginWith2faViewModel
{
    [Required(ErrorMessage = "Enter the code.")]
    [StringLength(20)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>True when the person types one of their recovery codes instead (lost phone).</summary>
    public bool UseRecoveryCode { get; set; }

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>22 Nov 2026 (docs/41, H2): the "Two-step sign-in" page: status, set-up (QR code and key) and recovery codes.</summary>
public class TwoStepViewModel
{
    public bool Enabled { get; set; }

    /// <summary>The person's role must use it (and the rule is on): they cannot turn it off.</summary>
    public bool Required { get; set; }

    /// <summary>They were sent here because they must set it up before using GRSMS.</summary>
    public bool MustSetUp { get; set; }

    /// <summary>For setting up: the key in groups of four, and the same key as a QR code (PNG data URI).</summary>
    public string? SharedKey { get; set; }
    public string? QrDataUri { get; set; }

    public int RecoveryCodesLeft { get; set; }

    /// <summary>New recovery codes, shown once right after they are made.</summary>
    public IReadOnlyList<string>? NewRecoveryCodes { get; set; }
}
