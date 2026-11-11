using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using static Grsms.Application.Common.Text;
using static Grsms.Application.Common.Signatures;

namespace Grsms.Application.Onboarding;

/// <summary>
/// Phase 19: the Employee Personal Record Declaration (<see cref="EmployeePersonalRecord"/>).
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * Fill in / change, sign : the new employee, on their own onboarding, while it is their turn (Draft or Awaiting
///                              employee submission). Changing a signed record clears the signature: it must be signed again.
///   * Read                   : the new employee (their own) and HR (Recruitment Officer, HR Manager). Not the HOD: it
///                              holds family, bank and health details.
/// Every rule of the form (required fields, at least one language, bank account, emergency contact and beneficiary, exactly
/// exactly one bank account (bank name and account number), and beneficiaries adding up to exactly 100%) is checked here; nothing is saved otherwise.
/// Each signature is an immutable <see cref="EmployeeDeclaration"/> holding a frozen copy of what was signed.
/// </summary>
public class PersonalRecordService(
    IRepository<OnboardingRecord> onboardings,
    IPersonalRecordRepository records,
    IRepository<EmployeeDeclaration> declarations,
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    OnboardingProgressService progress)
{
    /// <summary>The words the employee signs.</summary>
    public const string DeclarationText = "I declare that the above information is accurate and correct to the best of my knowledge.";

    private const int MaxRows = 30, MaxSpouses = 4, MaxEmergencyContacts = 3;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private bool MayView => me.HasPermission(Domain.Security.Permissions.ViewOnboarding); // Phase 27: onboarding.view

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ===================================================================== Reading

    /// <summary>The new employee's own record, for one of their applications (null if there is no onboarding or it is not theirs).</summary>
    public async Task<PersonalRecordPageDto?> GetMineAsync(int applicationId, CancellationToken ct = default)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId || o.Status == OnboardingStatus.Cancelled) return null;
        return await PageAsync(o, OnboardingWorkflow.EmployeeMayEditDocuments(o.Status), ct);
    }

    /// <summary>HR's read-only view (null for anyone else).</summary>
    public async Task<PersonalRecordPageDto?> GetForHrAsync(int onboardingId, CancellationToken ct = default)
    {
        if (!MayView) return null;
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        return o is null || o.EmployeeUserId == me.UserId ? null : await PageAsync(o, canEdit: false, ct);
    }

    private async Task<PersonalRecordPageDto> PageAsync(OnboardingRecord o, bool canEdit, CancellationToken ct)
    {
        var r = await records.GetForOnboardingAsync(o.Id, ct);
        var signed = r is null ? [] : (await declarations.ListAsync(d => d.PersonalRecordId == r.Id, ct))
            .OrderByDescending(d => d.SignedAt).Select(d => new DeclarationDto(d.Id, d.SignatureName, d.SignedAt, d.IpAddress, d.SnapshotSha256)).ToList();
        return new PersonalRecordPageDto
        {
            OnboardingId = o.Id,
            ApplicationId = o.JobApplicationId,
            OnboardingNumber = o.Number,
            EmployeeName = o.EmployeeName,
            StaffNumber = o.EmployeeNumber,
            DepartmentName = (await departments.GetByIdAsync(o.DepartmentId, ct))?.Name ?? "",
            Position = o.JobTitle,
            ServiceDate = o.StartDate,
            Age = r?.AgeOn(Today),
            Form = r is null ? NewForm() : ToForm(r),
            Saved = r is not null,
            DeclaredAt = r?.DeclaredAt,
            Declarations = signed,
            CanEdit = canEdit
        };
    }

    /// <summary>A blank form with one empty row in each list that needs one.</summary>
    public static PersonalRecordForm NewForm() => new()
    {
        Languages = [new()], BankAccounts = [new() { IsPrimary = true }], EmergencyContacts = [new()], Beneficiaries = [new()]
    };

    // ===================================================================== Saving and signing (the new employee)

    /// <summary>Saves the whole form (all-or-nothing). A signed record must be signed again afterwards.</summary>
    public async Task<Result> SaveAsync(int applicationId, PersonalRecordForm form, CancellationToken ct = default)
    {
        var (o, error) = await LoadMineForEditAsync(applicationId, ct);
        if (o is null) return Result.Failure(error!);
        Normalize(form);
        var errors = Validate(form);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        var r = await records.GetForOnboardingAsync(o.Id, ct);
        if (r is null)
        {
            r = new EmployeePersonalRecord { OnboardingRecordId = o.Id, EmployeeUserId = o.EmployeeUserId };
            records.Add(r);
        }
        Apply(form, r);
        r.DeclaredAt = null;            // any change needs a new signature
        r.CurrentDeclarationId = null;
        r.Version = Guid.NewGuid();
        var saved = await SaveChangesAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o.Id, ct); // Phase 27: unsigned again → back with the new employee
        return saved;
    }

    /// <summary>
    /// Signs the declaration by typing the full name exactly as on the onboarding. Stores an immutable declaration with the
    /// date, the IP address and a frozen copy (JSON + SHA-256) of the record.
    /// </summary>
    public async Task<Result> SignAsync(int applicationId, string? typedName, bool confirmed, CancellationToken ct = default)
    {
        var (o, error) = await LoadMineForEditAsync(applicationId, ct);
        if (o is null) return Result.Failure(error!);
        var r = await records.GetForOnboardingAsync(o.Id, ct);
        if (r is null) return Result.Failure("Fill in and save your personal record first.");
        if (r.DeclaredAt is not null) return Result.Failure("You have already signed this version of your personal record.");
        if (!confirmed) return Result.Failure("Tick the box to confirm the declaration.");
        typedName = Clean(typedName);
        if (typedName is null || !SameName(typedName, o.EmployeeName))
            return Result.Failure($"Type your full name exactly as it appears here ({o.EmployeeName}) to sign.");
        // The saved record must still pass every rule (e.g. nothing changed in the rules since it was saved).
        var errors = Validate(ToForm(r));
        if (errors.Count > 0) return Result.Failure([.. errors]);

        var snapshot = JsonSerializer.Serialize(new
        {
            o.EmployeeName, StaffNumber = o.EmployeeNumber, Position = o.JobTitle, ServiceDate = o.StartDate, Record = ToForm(r)
        }, SnapshotJson);
        var now = DateTime.UtcNow;
        var declaration = new EmployeeDeclaration
        {
            PersonalRecordId = r.Id,
            EmployeeUserId = o.EmployeeUserId,
            DeclarationText = DeclarationText,
            SignatureName = typedName,
            SignedAt = now,
            IpAddress = me.IpAddress,
            Snapshot = snapshot,
            SnapshotSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant()
        };
        declarations.Add(declaration);
        var saved = await SaveChangesAsync(ct);
        if (!saved.Succeeded) return saved;

        r.DeclaredAt = now;
        r.CurrentDeclarationId = declaration.Id;
        r.Version = Guid.NewGuid();
        saved = await SaveChangesAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o.Id, ct); // Phase 27
        return saved;
    }

    // ===================================================================== Rules

    /// <summary>Every rule of the form. Returns all problems at once.</summary>
    public static List<string> Validate(PersonalRecordForm f)
    {
        var errors = new List<string>();
        void Need(object? value, string label)
        {
            if (value is null || value is string s && s.Length == 0) errors.Add($"Enter your {label}.");
        }

        // Employee information
        Need(f.SsnitNumber, "SSNIT number");
        Need(f.DateOfBirth, "date of birth");
        if (f.DateOfBirth is { } dob)
        {
            var age = EmployeePersonalRecord.AgeBetween(dob, Today);
            if (age is < 15 or > 75) errors.Add("Check your date of birth: it gives an age of " + age + ".");
        }
        Need(f.ResidentialAddress, "residential address");
        Need(f.HomeTown, "home town");
        Need(f.Region, "region");
        Need(f.MobileNumber, "mobile number");
        if (f.MobileNumber is not null && PhoneNumbers.ToInternational(f.MobileNumber) is null) errors.Add("Check your mobile number.");
        if (f.Email is not null && !MailAddress.TryCreate(f.Email, out _)) errors.Add("Check your email address.");
        if (f.HasDisability && f.DisabilityDetails is null) errors.Add("Describe the disability (and any support you need at work).");

        // Next of kin
        if (f.NextOfKinName is null || f.NextOfKinRelationship is null || f.NextOfKinTelephone is null)
            errors.Add("Give your next of kin's name, relationship and telephone number.");
        else if (PhoneNumbers.ToInternational(f.NextOfKinTelephone) is null) errors.Add("Check your next of kin's telephone number.");
        if (f.NextOfKinDateOfBirth > Today) errors.Add("Your next of kin's date of birth cannot be in the future.");

        // Languages
        if (f.Languages.Count == 0) errors.Add("Add at least one language you speak.");
        if (f.Languages.Any(l => l.Language is null || l.Proficiency is null)) errors.Add("Give each language and how well you speak it.");
        if (f.Languages.Select(l => l.Language?.ToLowerInvariant()).Distinct().Count() != f.Languages.Count) errors.Add("List each language once.");

        // Bank account (11 Nov 2026, docs/29 change 4): exactly one, bank name and account number.
        if (f.BankAccounts.Count == 0) errors.Add("Give the bank account your salary should be paid into.");
        if (f.BankAccounts.Count > 1) errors.Add("Give one bank account only.");
        if (f.BankAccounts.Any(b => b.BankName is null || b.AccountNumber is null))
            errors.Add("Give the bank name and the account number.");

        // Family
        if (f.Spouses.Count > MaxSpouses) errors.Add($"List at most {MaxSpouses} spouses.");
        if (f.Spouses.Any(s => s.FullName is null)) errors.Add("Give each spouse's full name.");
        if (f.Spouses.Any(s => s.DateOfBirth > Today)) errors.Add("A spouse's date of birth cannot be in the future.");
        if (f.Children.Any(c => c.FullName is null || c.Gender is null || c.DateOfBirth is null))
            errors.Add("Give each child's full name, gender and date of birth.");
        if (f.Children.Any(c => c.DateOfBirth > Today)) errors.Add("A child's date of birth cannot be in the future.");
        if (f.Dependants.Any(d => d.FullName is null || d.Relationship is null || d.Age is null))
            errors.Add("Give each dependant's full name, relationship and age.");
        if (f.Dependants.Any(d => d.Age is < 0 or > 120)) errors.Add("A dependant's age must be between 0 and 120.");

        // Emergency contacts
        if (f.EmergencyContacts.Count == 0) errors.Add("Add at least one emergency contact.");
        if (f.EmergencyContacts.Count > MaxEmergencyContacts) errors.Add($"List at most {MaxEmergencyContacts} emergency contacts.");
        if (f.EmergencyContacts.Any(e => e.Name is null || e.Relationship is null || e.Telephone1 is null))
            errors.Add("Give each emergency contact's name, relationship and telephone number.");
        if (f.EmergencyContacts.Any(e => e.Telephone1 is not null && PhoneNumbers.ToInternational(e.Telephone1) is null ||
                                         e.Telephone2 is not null && PhoneNumbers.ToInternational(e.Telephone2) is null))
            errors.Add("Check the emergency contacts' telephone numbers.");

        // Beneficiaries: the shares must add up to exactly 100%.
        if (f.Beneficiaries.Count == 0) errors.Add("Add at least one beneficiary.");
        if (f.Beneficiaries.Any(b => b.Name is null || b.Relationship is null || b.Percentage is null))
            errors.Add("Give each beneficiary's name, relationship and percentage.");
        if (f.Beneficiaries.Any(b => b.Percentage is <= 0 or > 100)) errors.Add("Each beneficiary's percentage must be more than 0 and at most 100.");
        if (f.Beneficiaries.Any(b => b.DateOfBirth > Today)) errors.Add("A beneficiary's date of birth cannot be in the future.");
        var total = f.Beneficiaries.Sum(b => b.Percentage ?? 0);
        if (f.Beneficiaries.Count > 0 && total != 100m)
            errors.Add($"The beneficiaries' percentages add up to {total:0.##}%. They must add up to exactly 100%.");

        foreach (var (count, label) in new[] { (f.Languages.Count, "languages"), (f.BankAccounts.Count, "bank accounts"), (f.Children.Count, "children"),
                     (f.Dependants.Count, "dependants"), (f.Beneficiaries.Count, "beneficiaries") })
            if (count > MaxRows) errors.Add($"List at most {MaxRows} {label}.");
        return errors;
    }

    // ===================================================================== Helpers

    private async Task<(OnboardingRecord? Onboarding, string? Error)> LoadMineForEditAsync(int applicationId, CancellationToken ct)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId || o.Status == OnboardingStatus.Cancelled) return (null, "Personal record not found.");
        return OnboardingWorkflow.EmployeeMayEditDocuments(o.Status)
            ? (o, null)
            : (null, "Your personal record is with HR now; you can change it only if HR sends your onboarding back to you.");
    }

    /// <summary>Trims everything, drops completely empty rows, and makes the bank account the primary one.</summary>
    private static void Normalize(PersonalRecordForm f)
    {
        f.Qualification = Clean(f.Qualification);
        f.SsnitNumber = Clean(f.SsnitNumber)?.ToUpperInvariant();
        f.ResidentialAddress = Clean(f.ResidentialAddress);
        f.HomeTown = Clean(f.HomeTown);
        f.Tribe = Clean(f.Tribe);
        f.Religion = Clean(f.Religion);
        f.Email = Clean(f.Email);
        f.MobileNumber = Clean(f.MobileNumber);
        f.DisabilityDetails = f.HasDisability ? Clean(f.DisabilityDetails) : null;
        f.VisibleMarks = Clean(f.VisibleMarks);
        f.NextOfKinName = Clean(f.NextOfKinName);
        f.NextOfKinTelephone = Clean(f.NextOfKinTelephone);
        f.NextOfKinRelationship = Clean(f.NextOfKinRelationship);
        f.NextOfKinAddress = Clean(f.NextOfKinAddress);

        f.Languages = f.Languages.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.Languages) x.Language = Clean(x.Language);
        f.BankAccounts = f.BankAccounts.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.BankAccounts) { x.BankName = Clean(x.BankName); x.AccountNumber = Clean(x.AccountNumber); x.IsPrimary = true; }
        if (f.BankAccounts.Count == 1) f.BankAccounts[0].IsPrimary = true;
        f.Spouses = f.Spouses.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.Spouses) { x.FullName = Clean(x.FullName); x.Occupation = Clean(x.Occupation); x.Address = Clean(x.Address); x.Telephone = Clean(x.Telephone); }
        f.Children = f.Children.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.Children) { x.FullName = Clean(x.FullName); x.PlaceOfBirth = Clean(x.PlaceOfBirth); x.CurrentResidence = Clean(x.CurrentResidence); }
        f.Dependants = f.Dependants.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.Dependants) { x.FullName = Clean(x.FullName); x.Relationship = Clean(x.Relationship); }
        f.EmergencyContacts = f.EmergencyContacts.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.EmergencyContacts) { x.Name = Clean(x.Name); x.Relationship = Clean(x.Relationship); x.Telephone1 = Clean(x.Telephone1); x.Telephone2 = Clean(x.Telephone2); }
        f.Beneficiaries = f.Beneficiaries.Where(x => !x.IsEmpty).ToList();
        foreach (var x in f.Beneficiaries) { x.Name = Clean(x.Name); x.Relationship = Clean(x.Relationship); }
    }

    private static void Apply(PersonalRecordForm f, EmployeePersonalRecord r)
    {
        r.Qualification = f.Qualification;
        r.SsnitNumber = f.SsnitNumber;
        r.DateOfBirth = f.DateOfBirth;
        r.ResidentialAddress = f.ResidentialAddress;
        r.HomeTown = f.HomeTown;
        r.Region = f.Region;
        r.Tribe = f.Tribe;
        r.Religion = f.Religion;
        r.Email = f.Email;
        r.MobileNumber = f.MobileNumber;
        r.HasDisability = f.HasDisability;
        r.DisabilityDetails = f.DisabilityDetails;
        r.VisibleMarks = f.VisibleMarks;
        r.NextOfKinName = f.NextOfKinName;
        r.NextOfKinTelephone = f.NextOfKinTelephone;
        r.NextOfKinRelationship = f.NextOfKinRelationship;
        r.NextOfKinDateOfBirth = f.NextOfKinDateOfBirth;
        r.NextOfKinAddress = f.NextOfKinAddress;

        // The lists are replaced as a whole.
        r.Languages.Clear();
        foreach (var x in f.Languages) r.Languages.Add(new EmployeeLanguage { Language = x.Language!, Proficiency = x.Proficiency!.Value });
        r.BankAccounts.Clear();
        foreach (var x in f.BankAccounts) r.BankAccounts.Add(new EmployeeBankAccount { BankName = x.BankName!, AccountNumber = x.AccountNumber!, IsPrimary = true });
        r.Spouses.Clear();
        foreach (var x in f.Spouses) r.Spouses.Add(new EmployeeSpouse { FullName = x.FullName!, Occupation = x.Occupation, DateOfBirth = x.DateOfBirth, Address = x.Address, Telephone = x.Telephone });
        r.Children.Clear();
        foreach (var x in f.Children) r.Children.Add(new EmployeeChild { FullName = x.FullName!, Gender = x.Gender!.Value, DateOfBirth = x.DateOfBirth!.Value, PlaceOfBirth = x.PlaceOfBirth, CurrentResidence = x.CurrentResidence });
        r.Dependants.Clear();
        foreach (var x in f.Dependants) r.Dependants.Add(new EmployeeDependant { FullName = x.FullName!, Relationship = x.Relationship!, Age = x.Age!.Value });
        r.EmergencyContacts.Clear();
        foreach (var x in f.EmergencyContacts) r.EmergencyContacts.Add(new EmployeeEmergencyContact { Name = x.Name!, Relationship = x.Relationship!, Telephone1 = x.Telephone1!, Telephone2 = x.Telephone2 });
        r.Beneficiaries.Clear();
        foreach (var x in f.Beneficiaries) r.Beneficiaries.Add(new EmployeeBeneficiary { Name = x.Name!, Relationship = x.Relationship!, DateOfBirth = x.DateOfBirth, Percentage = x.Percentage!.Value });
    }

    public static PersonalRecordForm ToForm(EmployeePersonalRecord r) => new()
    {
        Qualification = r.Qualification, SsnitNumber = r.SsnitNumber, DateOfBirth = r.DateOfBirth, ResidentialAddress = r.ResidentialAddress,
        HomeTown = r.HomeTown, Region = r.Region, Tribe = r.Tribe, Religion = r.Religion, Email = r.Email, MobileNumber = r.MobileNumber,
        HasDisability = r.HasDisability, DisabilityDetails = r.DisabilityDetails, VisibleMarks = r.VisibleMarks,
        NextOfKinName = r.NextOfKinName, NextOfKinTelephone = r.NextOfKinTelephone, NextOfKinRelationship = r.NextOfKinRelationship,
        NextOfKinDateOfBirth = r.NextOfKinDateOfBirth, NextOfKinAddress = r.NextOfKinAddress,
        Languages = r.Languages.OrderBy(x => x.Id).Select(x => new LanguageRow { Language = x.Language, Proficiency = x.Proficiency }).ToList(),
        // One account since 11 Nov 2026: an older record with several shows (and keeps, when saved again) its primary one.
        BankAccounts = r.BankAccounts.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Id).Take(1)
            .Select(x => new BankAccountRow { BankName = x.BankName, AccountNumber = x.AccountNumber, IsPrimary = true }).ToList(),
        Spouses = r.Spouses.OrderBy(x => x.Id).Select(x => new SpouseRow { FullName = x.FullName, Occupation = x.Occupation, DateOfBirth = x.DateOfBirth, Address = x.Address, Telephone = x.Telephone }).ToList(),
        Children = r.Children.OrderBy(x => x.Id).Select(x => new ChildRow { FullName = x.FullName, Gender = x.Gender, DateOfBirth = x.DateOfBirth, PlaceOfBirth = x.PlaceOfBirth, CurrentResidence = x.CurrentResidence }).ToList(),
        Dependants = r.Dependants.OrderBy(x => x.Id).Select(x => new DependantRow { FullName = x.FullName, Relationship = x.Relationship, Age = x.Age }).ToList(),
        EmergencyContacts = r.EmergencyContacts.OrderBy(x => x.Id).Select(x => new EmergencyContactRow { Name = x.Name, Relationship = x.Relationship, Telephone1 = x.Telephone1, Telephone2 = x.Telephone2 }).ToList(),
        Beneficiaries = r.Beneficiaries.OrderBy(x => x.Id).Select(x => new BeneficiaryRow { Name = x.Name, Relationship = x.Relationship, DateOfBirth = x.DateOfBirth, Percentage = x.Percentage }).ToList()
    };



    private Task<Result> SaveChangesAsync(CancellationToken ct) => unitOfWork.TrySaveAsync("Your personal record was changed at the same moment elsewhere. Please reload the page and try again.", ct);
}
