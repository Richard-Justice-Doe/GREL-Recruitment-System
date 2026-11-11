using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Onboarding;

// Phase 19: the GREL Employee Personal Record Declaration form, filled in by the new employee during onboarding.

/// <summary>Ghana's sixteen regions (plus "Outside Ghana" for non-citizens' home towns).</summary>
public enum GhanaRegion
{
    [Display(Name = "Ahafo")] Ahafo,
    [Display(Name = "Ashanti")] Ashanti,
    [Display(Name = "Bono")] Bono,
    [Display(Name = "Bono East")] BonoEast,
    [Display(Name = "Central")] Central,
    [Display(Name = "Eastern")] Eastern,
    [Display(Name = "Greater Accra")] GreaterAccra,
    [Display(Name = "North East")] NorthEast,
    [Display(Name = "Northern")] Northern,
    [Display(Name = "Oti")] Oti,
    [Display(Name = "Savannah")] Savannah,
    [Display(Name = "Upper East")] UpperEast,
    [Display(Name = "Upper West")] UpperWest,
    [Display(Name = "Volta")] Volta,
    [Display(Name = "Western")] Western,
    [Display(Name = "Western North")] WesternNorth,
    [Display(Name = "Outside Ghana")] OutsideGhana
}

/// <summary>How well the employee speaks a language.</summary>
public enum LanguageProficiency
{
    [Display(Name = "Very fluent")] VeryFluent,
    [Display(Name = "Fluent")] Fluent,
    [Display(Name = "Passable")] Passable
}

public enum Gender
{
    [Display(Name = "Male")] Male,
    [Display(Name = "Female")] Female
}

/// <summary>
/// The new employee's Personal Record Declaration: one per onboarding. The employee fills it in in their portal while it is
/// their turn (see <see cref="OnboardingWorkflow.EmployeeMayEditDocuments"/>) and signs the declaration; any later change
/// clears the signature, so it must be signed again. Every signature is kept as an <see cref="EmployeeDeclaration"/>
/// with a frozen copy of what was signed. Only the employee and HR can see it (personal and family data).
///
/// The lists (languages, bank accounts, spouses, children, dependants, emergency contacts, beneficiaries) are replaced as
/// a whole on every save, like a vacancy's requirements; the audit trail records every row added or removed.
/// </summary>
public class EmployeePersonalRecord : BaseEntity
{
    /// <summary>One per onboarding (unique).</summary>
    public int OnboardingRecordId { get; set; }

    /// <summary>The employee (Users.Id).</summary>
    public string EmployeeUserId { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Employee information
    // Name, staff number, department, position and service date come from the onboarding and are not typed again.

    /// <summary>Highest qualification, e.g. "BSc Agriculture, KNUST".</summary>
    public string? Qualification { get; set; }

    public string? SsnitNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? ResidentialAddress { get; set; }
    public string? HomeTown { get; set; }
    public GhanaRegion? Region { get; set; }
    public string? Tribe { get; set; }
    public string? Religion { get; set; }

    /// <summary>Personal email (the GRSMS sign-in email is used if left empty).</summary>
    public string? Email { get; set; }

    public string? MobileNumber { get; set; }

    public bool HasDisability { get; set; }

    /// <summary>The disability and any support needed at work, when <see cref="HasDisability"/>.</summary>
    public string? DisabilityDetails { get; set; }

    /// <summary>Visible identifying marks (scars, tribal marks, ...), as on the paper form.</summary>
    public string? VisibleMarks { get; set; }

    // ---------------------------------------------------------------- Next of kin (one)
    public string? NextOfKinName { get; set; }
    public string? NextOfKinTelephone { get; set; }
    public string? NextOfKinRelationship { get; set; }
    public DateOnly? NextOfKinDateOfBirth { get; set; }
    public string? NextOfKinAddress { get; set; }

    // ---------------------------------------------------------------- Lists
    public ICollection<EmployeeLanguage> Languages { get; set; } = new List<EmployeeLanguage>();
    public ICollection<EmployeeBankAccount> BankAccounts { get; set; } = new List<EmployeeBankAccount>();
    public ICollection<EmployeeSpouse> Spouses { get; set; } = new List<EmployeeSpouse>();
    public ICollection<EmployeeChild> Children { get; set; } = new List<EmployeeChild>();
    public ICollection<EmployeeDependant> Dependants { get; set; } = new List<EmployeeDependant>();
    public ICollection<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new List<EmployeeEmergencyContact>();
    public ICollection<EmployeeBeneficiary> Beneficiaries { get; set; } = new List<EmployeeBeneficiary>();

    // ---------------------------------------------------------------- Declaration
    /// <summary>When the current version was signed; null when it was never signed or changed since (must be signed again).</summary>
    public DateTime? DeclaredAt { get; set; }

    /// <summary>The signature currently in force (EmployeeDeclarations.Id).</summary>
    public int? CurrentDeclarationId { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>Age in whole years on <paramref name="today"/>, from the date of birth.</summary>
    public int? AgeOn(DateOnly today) => DateOfBirth is { } dob ? AgeBetween(dob, today) : null;

    public static int AgeBetween(DateOnly born, DateOnly on) =>
        on.Year - born.Year - (on < born.AddYears(on.Year - born.Year) ? 1 : 0);
}

public class EmployeeLanguage
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string Language { get; set; } = string.Empty;
    public LanguageProficiency Proficiency { get; set; }
}

public class EmployeeBankAccount
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string BankName { get; set; } = string.Empty;
    /// <summary>No longer captured since 11 Nov 2026 (docs/29): kept for accounts given before then.</summary>
    public string? Branch { get; set; }
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>The account salary is paid into. Since 11 Nov 2026 an employee has exactly one account, always primary.</summary>
    public bool IsPrimary { get; set; }
}

public class EmployeeSpouse
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Telephone { get; set; }
}

public class EmployeeChild
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? CurrentResidence { get; set; }
}

public class EmployeeDependant
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class EmployeeEmergencyContact
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Telephone1 { get; set; } = string.Empty;
    public string? Telephone2 { get; set; }
}

public class EmployeeBeneficiary
{
    public int Id { get; set; }
    public int PersonalRecordId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Share of benefits, e.g. 50.00. All of an employee's beneficiaries add up to exactly 100.</summary>
    public decimal Percentage { get; set; }
}

/// <summary>
/// One signature of the Personal Record Declaration. Never changed or deleted (an immutable record): signing again adds a
/// new row. <see cref="Snapshot"/> is a frozen copy (JSON) of everything that was declared at that moment.
/// </summary>
public class EmployeeDeclaration : BaseEntity
{
    public int PersonalRecordId { get; set; }
    public string EmployeeUserId { get; set; } = string.Empty;

    /// <summary>"I declare that the above information is accurate and correct to the best of my knowledge."</summary>
    public string DeclarationText { get; set; } = string.Empty;

    /// <summary>The full name the employee typed to sign.</summary>
    public string SignatureName { get; set; } = string.Empty;

    public DateTime SignedAt { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>The whole personal record as signed (JSON), so later edits can never change what was declared.</summary>
    public string Snapshot { get; set; } = string.Empty;

    /// <summary>SHA-256 of <see cref="Snapshot"/>, to show it has not been altered.</summary>
    public string SnapshotSha256 { get; set; } = string.Empty;
}
