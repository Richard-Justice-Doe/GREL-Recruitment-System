using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

// Phase 19: the Employee Personal Record Declaration form. Every field is checked by PersonalRecordService; the
// attributes here only label the fields and cap their length. List rows that are left completely empty are ignored.

public class LanguageRow
{
    [StringLength(60)] public string? Language { get; set; }
    public LanguageProficiency? Proficiency { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Language) && Proficiency is null;
}

public class BankAccountRow
{
    [StringLength(100)] public string? BankName { get; set; }
    [StringLength(100)] public string? Branch { get; set; }
    [StringLength(40)] public string? AccountNumber { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(BankName) && string.IsNullOrWhiteSpace(Branch) && string.IsNullOrWhiteSpace(AccountNumber);
}

public class SpouseRow
{
    [StringLength(200)] public string? FullName { get; set; }
    [StringLength(100)] public string? Occupation { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(30)] public string? Telephone { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(FullName) && string.IsNullOrWhiteSpace(Occupation) && DateOfBirth is null &&
                           string.IsNullOrWhiteSpace(Address) && string.IsNullOrWhiteSpace(Telephone);
}

public class ChildRow
{
    [StringLength(200)] public string? FullName { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [StringLength(100)] public string? PlaceOfBirth { get; set; }
    [StringLength(200)] public string? CurrentResidence { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(FullName) && Gender is null && DateOfBirth is null &&
                           string.IsNullOrWhiteSpace(PlaceOfBirth) && string.IsNullOrWhiteSpace(CurrentResidence);
}

public class DependantRow
{
    [StringLength(200)] public string? FullName { get; set; }
    [StringLength(100)] public string? Relationship { get; set; }
    public int? Age { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(FullName) && string.IsNullOrWhiteSpace(Relationship) && Age is null;
}

public class EmergencyContactRow
{
    [StringLength(200)] public string? Name { get; set; }
    [StringLength(100)] public string? Relationship { get; set; }
    [StringLength(30)] public string? Telephone1 { get; set; }
    [StringLength(30)] public string? Telephone2 { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Relationship) &&
                           string.IsNullOrWhiteSpace(Telephone1) && string.IsNullOrWhiteSpace(Telephone2);
}

public class BeneficiaryRow
{
    [StringLength(200)] public string? Name { get; set; }
    [StringLength(100)] public string? Relationship { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public decimal? Percentage { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Relationship) && DateOfBirth is null && Percentage is null;
}

/// <summary>The Employee Personal Record Declaration as the new employee fills it in.</summary>
public class PersonalRecordForm
{
    // ---- Employee information
    [StringLength(200)] [Display(Name = "Qualification")] public string? Qualification { get; set; }
    [StringLength(30)] [Display(Name = "SSNIT number")] public string? SsnitNumber { get; set; }
    [DataType(DataType.Date)] [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [StringLength(500)] [Display(Name = "Residential address")] public string? ResidentialAddress { get; set; }
    [StringLength(100)] [Display(Name = "Home town")] public string? HomeTown { get; set; }
    [Display(Name = "Region")] public GhanaRegion? Region { get; set; }
    [StringLength(100)] [Display(Name = "Tribe")] public string? Tribe { get; set; }
    [StringLength(100)] [Display(Name = "Religion")] public string? Religion { get; set; }
    [StringLength(256)] [Display(Name = "Email")] public string? Email { get; set; }
    [StringLength(30)] [Display(Name = "Mobile number")] public string? MobileNumber { get; set; }
    [Display(Name = "I have a disability")] public bool HasDisability { get; set; }
    [StringLength(2000)] [Display(Name = "Disability (and any support you need at work)")] public string? DisabilityDetails { get; set; }
    [StringLength(500)] [Display(Name = "Visible marks")] public string? VisibleMarks { get; set; }

    // ---- Next of kin
    [StringLength(200)] [Display(Name = "Name")] public string? NextOfKinName { get; set; }
    [StringLength(30)] [Display(Name = "Telephone")] public string? NextOfKinTelephone { get; set; }
    [StringLength(100)] [Display(Name = "Relationship")] public string? NextOfKinRelationship { get; set; }
    [DataType(DataType.Date)] [Display(Name = "Date of birth")] public DateOnly? NextOfKinDateOfBirth { get; set; }
    [StringLength(500)] [Display(Name = "Address")] public string? NextOfKinAddress { get; set; }

    // ---- Lists
    public List<LanguageRow> Languages { get; set; } = [];
    public List<BankAccountRow> BankAccounts { get; set; } = [];
    public List<SpouseRow> Spouses { get; set; } = [];
    public List<ChildRow> Children { get; set; } = [];
    public List<DependantRow> Dependants { get; set; } = [];
    public List<EmergencyContactRow> EmergencyContacts { get; set; } = [];
    public List<BeneficiaryRow> Beneficiaries { get; set; } = [];
}

/// <summary>One signature of the declaration, for the history.</summary>
public record DeclarationDto(int Id, string SignatureName, DateTime SignedAt, string? IpAddress, string SnapshotSha256);

/// <summary>The personal record page (the new employee's form, or HR's read-only view).</summary>
public record PersonalRecordPageDto
{
    public int OnboardingId { get; init; }
    public int ApplicationId { get; init; }
    public string OnboardingNumber { get; init; } = "";

    // Taken from the onboarding, not typed again
    public string EmployeeName { get; init; } = "";
    public string? StaffNumber { get; init; }
    public string DepartmentName { get; init; } = "";
    public string Position { get; init; } = "";
    public DateOnly ServiceDate { get; init; }
    public int? Age { get; init; }

    public PersonalRecordForm Form { get; init; } = new();

    /// <summary>True once the form has been saved at least once.</summary>
    public bool Saved { get; init; }
    public DateTime? DeclaredAt { get; init; }
    public bool Signed => DeclaredAt is not null;
    public IReadOnlyList<DeclarationDto> Declarations { get; init; } = [];

    public bool CanEdit { get; init; }
    public bool CanSign => CanEdit && Saved && !Signed;
}

/// <summary>Loading a personal record with all its lists.</summary>
public interface IPersonalRecordRepository : IRepository<EmployeePersonalRecord>
{
    Task<EmployeePersonalRecord?> GetForOnboardingAsync(int onboardingId, CancellationToken ct = default);

    /// <summary>Phase 21: every personal record with its beneficiaries (Beneficiary report).</summary>
    Task<IReadOnlyList<EmployeePersonalRecord>> ListWithBeneficiariesAsync(CancellationToken ct = default);
}
