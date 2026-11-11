using System.Security.Cryptography;
using System.Text;
using Grsms.Application.Onboarding;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Tests;

/// <summary>
/// Phase 19: the Employee Personal Record Declaration: every rule of the form (beneficiaries add up to exactly 100%, one
/// primary bank account, required fields), the signed declaration (name, date, IP, frozen copy and fingerprint), signing again
/// after a change, signatures that can never be altered, who may see and edit it, and that HR needs it before verification.
/// </summary>
public class PersonalRecordServiceTests : OnboardingTestBase
{
    private async Task<(int Id, int App)> LetterSignedAsync()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        await IssueAndSignAsync(id, app);
        return (id, app);
    }

    private async Task<IReadOnlyList<string>> ErrorsFor(int app, Action<PersonalRecordForm> change)
    {
        var form = ValidRecord();
        change(form);
        As(_candidate, Roles.Candidate);
        return (await RunRecord(s => s.SaveAsync(app, form))).Errors;
    }

    [Fact]
    public async Task Beneficiaries_must_add_up_to_exactly_one_hundred_percent_or_nothing_is_saved()
    {
        var (id, app) = await LetterSignedAsync();

        Assert.Contains(await ErrorsFor(app, f => f.Beneficiaries[1].Percentage = 30m), e => e.Contains("add up to 90%"));
        Assert.Contains(await ErrorsFor(app, f => f.Beneficiaries[1].Percentage = 50m), e => e.Contains("add up to 110%"));
        Assert.Contains(await ErrorsFor(app, f => f.Beneficiaries.Clear()), e => e.Contains("at least one beneficiary"));
        Assert.Contains(await ErrorsFor(app, f => f.Beneficiaries[1].Percentage = 0m), e => e.Contains("more than 0"));
        Assert.Null(await Db(db => db.EmployeePersonalRecords.FirstOrDefaultAsync(r => r.OnboardingRecordId == id))); // nothing saved

        // 33.33 + 33.33 + 33.34 = 100 exactly: accepted.
        Assert.Empty(await ErrorsFor(app, f =>
        {
            f.Beneficiaries = [new() { Name = "A", Relationship = "Son", Percentage = 33.33m }, new() { Name = "B", Relationship = "Son", Percentage = 33.33m },
                               new() { Name = "C", Relationship = "Son", Percentage = 33.34m }];
        }));
        var saved = await Db(db => db.EmployeePersonalRecords.Include(r => r.Beneficiaries).SingleAsync(r => r.OnboardingRecordId == id));
        Assert.Equal(100m, saved.Beneficiaries.Sum(b => b.Percentage));
    }

    [Fact]
    public async Task The_form_checks_required_fields_bank_accounts_contacts_and_dates()
    {
        var (_, app) = await LetterSignedAsync();
        Assert.Contains(await ErrorsFor(app, f => f.SsnitNumber = " "), e => e.Contains("SSNIT"));
        Assert.Contains(await ErrorsFor(app, f => f.DateOfBirth = Today.AddYears(-5)), e => e.Contains("date of birth"));
        Assert.Contains(await ErrorsFor(app, f => f.MobileNumber = "12"), e => e.Contains("mobile number"));
        Assert.Contains(await ErrorsFor(app, f => f.Email = "not-an-email"), e => e.Contains("email"));
        Assert.Contains(await ErrorsFor(app, f => f.HasDisability = true), e => e.Contains("Describe the disability"));
        Assert.Contains(await ErrorsFor(app, f => f.NextOfKinName = null), e => e.Contains("next of kin"));
        Assert.Contains(await ErrorsFor(app, f => f.Languages.Clear()), e => e.Contains("language"));
        Assert.Contains(await ErrorsFor(app, f => f.EmergencyContacts.Clear()), e => e.Contains("emergency contact"));
        Assert.Contains(await ErrorsFor(app, f => f.Children[0].DateOfBirth = Today.AddDays(1)), e => e.Contains("child's date of birth"));
        Assert.Contains(await ErrorsFor(app, f =>
        {
            f.BankAccounts.Add(new() { BankName = "Ecobank", AccountNumber = "0020" });
            f.BankAccounts.ForEach(b => b.IsPrimary = true);
        }), e => e.Contains("one bank account only")); // 11 Nov 2026: exactly one account
        Assert.Contains(await ErrorsFor(app, f => f.BankAccounts[0].AccountNumber = null), e => e.Contains("bank name and the account number"));

        // Completely empty rows (e.g. the blank row shown on the page) are ignored; one account becomes the primary one.
        Assert.Empty(await ErrorsFor(app, f => { f.Children.Add(new()); f.Dependants.Add(new()); f.BankAccounts[0].IsPrimary = false; }));
        As(_candidate, Roles.Candidate);
        var page = (await RunRecord(s => s.GetMineAsync(app)))!;
        Assert.Single(page.Form.Children);
        Assert.True(page.Form.BankAccounts.Single().IsPrimary);
        Assert.Equal(29, page.Age);
    }

    [Fact]
    public async Task Signing_records_name_date_ip_and_a_frozen_copy_and_any_change_needs_a_new_signature()
    {
        var (id, app) = await LetterSignedAsync();
        As(_candidate, Roles.Candidate);
        Assert.False((await RunRecord(s => s.SignAsync(app, "Ama Mensah", true))).Succeeded); // nothing saved yet
        Assert.True((await RunRecord(s => s.SaveAsync(app, ValidRecord()))).Succeeded);
        Assert.False((await RunRecord(s => s.SignAsync(app, "Ama Mensah", false))).Succeeded);  // box not ticked
        Assert.False((await RunRecord(s => s.SignAsync(app, "A. Mensah", true))).Succeeded);   // not the full name
        Assert.True((await RunRecord(s => s.SignAsync(app, "ama  mensah", true))).Succeeded);

        var first = await Db(db => db.EmployeeDeclarations.SingleAsync());
        Assert.Equal(("ama  mensah", "10.0.0.1", PersonalRecordService.DeclarationText), (first.SignatureName, first.IpAddress, first.DeclarationText));
        Assert.Contains("\"Percentage\":60", first.Snapshot);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Snapshot))).ToLowerInvariant(), first.SnapshotSha256);
        Assert.True((await RunRecord(s => s.GetMineAsync(app)))!.Signed);
        Assert.False((await RunRecord(s => s.SignAsync(app, "Ama Mensah", true))).Succeeded); // already signed

        // A change clears the signature; the first declaration stays exactly as it was.
        var changed = ValidRecord();
        changed.Beneficiaries[0].Percentage = 50m;
        changed.Beneficiaries[1].Percentage = 50m;
        Assert.True((await RunRecord(s => s.SaveAsync(app, changed))).Succeeded);
        Assert.False((await RunRecord(s => s.GetMineAsync(app)))!.Signed);
        AsRecruiter();
        Assert.False((await Run(s => s.GetAsync(id)))!.PersonalRecordSigned); // what the RO sees
        As(_candidate, Roles.Candidate);
        Assert.True((await RunRecord(s => s.SignAsync(app, "Ama Mensah", true))).Succeeded);

        var all = await Db(db => db.EmployeeDeclarations.OrderBy(d => d.Id).ToListAsync());
        Assert.Equal(2, all.Count);
        Assert.Equal(first.Snapshot, all[0].Snapshot);
        Assert.Contains("\"Percentage\":50", all[1].Snapshot);
        Assert.Equal(all[1].Id, (await Db(db => db.EmployeePersonalRecords.SingleAsync())).CurrentDeclarationId);
    }

    [Fact]
    public async Task A_signed_declaration_can_never_be_changed_or_deleted()
    {
        var (_, app) = await LetterSignedAsync();
        await FillPersonalRecordAsync(app);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db(async db =>
        {
            (await db.EmployeeDeclarations.SingleAsync()).SignatureName = "Someone else";
            return await db.SaveChangesAsync();
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db(async db =>
        {
            db.EmployeeDeclarations.Remove(await db.EmployeeDeclarations.SingleAsync());
            return await db.SaveChangesAsync();
        }));
        Assert.Equal("Ama Mensah", (await Db(db => db.EmployeeDeclarations.SingleAsync())).SignatureName);
    }

    [Fact]
    public async Task Only_the_employee_edits_it_until_activation_and_only_they_and_hr_see_it()
    {
        var id = await ReadyAsync(); // personal record signed, documents verified
        var app = (await RecordAsync(id)).JobApplicationId;

        // Phase 27: until activation the employee may still change it (and must then sign again).
        As(_candidate, Roles.Candidate);
        Assert.True((await RunRecord(s => s.GetMineAsync(app)))!.CanEdit);

        // Others cannot see it at all; HR (onboarding.view) can.
        As(_candidate2, Roles.Candidate);
        Assert.Null(await RunRecord(s => s.GetMineAsync(app)));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Null(await RunRecord(s => s.GetForHrAsync(id)));
        Assert.Null((await Run(s => s.GetAsync(id)))!.PersonalRecordSigned);
        AsHrManager();
        var hr = (await RunRecord(s => s.GetForHrAsync(id)))!;
        Assert.Equal(("Ama Mensah", "C123456789012", 2), (hr.EmployeeName, hr.Form.SsnitNumber, hr.Form.Beneficiaries.Count));
        Assert.Single(hr.Declarations);
        Assert.True((await Run(s => s.GetAsync(id)))!.PersonalRecordSigned);

        // Once activated, the employee can read it but not change it.
        AsRecruiter();
        Assert.True((await Run(s => s.ActivateAsync(id, "GREL-0457"))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.False((await RunRecord(s => s.GetMineAsync(app)))!.CanEdit);
        Assert.False((await RunRecord(s => s.SaveAsync(app, ValidRecord()))).Succeeded);
    }
}
