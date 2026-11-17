using Grsms.Application.Employees;
using Grsms.Domain.Documents;
using Grsms.Domain.Employees;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// 17 November 2026 (employee directory and profile): photos (HR only, JPEG/PNG up to 3 MB, the verified onboarding photograph as
/// a fallback, same visibility as the record), the profile completion from what GRSMS holds, the pending actions, and the team.
/// </summary>
public class EmployeeProfileTests : OnboardingTestBase
{
    /// <summary>A real 1 × 1 PNG.</summary>
    private static MemoryStream Png() => new(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));

    private async Task<T> Svc<T, TService>(Func<TService, Task<T>> call) where TService : notnull
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private Task<Employee> EmployeeAsync() => Db(db => db.Employees.SingleAsync(e => e.UserId == _candidate));

    [Fact]
    public async Task Hr_uploads_a_photo_shown_to_those_who_may_open_the_record_and_initials_otherwise()
    {
        await ActivatedAsync();
        var e = await EmployeeAsync();

        // Only HR; only pictures.
        As(_staff, Roles.Employee);
        Assert.False((await Svc<Grsms.Application.Common.Result, EmployeePhotoService>(s => s.UploadAsync(e.Id, "me.png", Png()))).Succeeded);
        AsHrManager();
        Assert.Contains("JPEG or PNG", (await Svc<Grsms.Application.Common.Result, EmployeePhotoService>(s => s.UploadAsync(e.Id, "cv.pdf", FakePdf()))).Errors[0]);
        var up = await Svc<Grsms.Application.Common.Result, EmployeePhotoService>(s => s.UploadAsync(e.Id, "ama.png", Png()));
        Assert.True(up.Succeeded, string.Join(" ", up.Errors));
        Assert.NotNull((await EmployeeAsync()).PhotoAttachmentId);

        var file = await Svc<Grsms.Application.Documents.FileDownload?, EmployeePhotoService>(s => s.OpenAsync(e.Id));
        Assert.Equal("image/png", file!.ContentType);
        await file.Content.DisposeAsync();

        // The Head of another department, or a colleague, gets nothing; the employee sees their own.
        As(_itHod, Roles.HeadOfDepartment);
        Assert.Null(await Svc<Grsms.Application.Documents.FileDownload?, EmployeePhotoService>(s => s.OpenAsync(e.Id)));
        As(_candidate, Roles.Employee);
        var own = await Svc<Grsms.Application.Documents.FileDownload?, EmployeePhotoService>(s => s.OpenAsync(e.Id));
        Assert.NotNull(own);
        await own!.Content.DisposeAsync();

        // Removed: back to initials.
        AsHrManager();
        Assert.True((await Svc<Grsms.Application.Common.Result, EmployeePhotoService>(s => s.RemoveAsync(e.Id))).Succeeded);
        Assert.Null(await Svc<Grsms.Application.Documents.FileDownload?, EmployeePhotoService>(s => s.OpenAsync(e.Id)));
        Assert.Equal(("AM", "KO", "?"), (EmployeePhotoService.Initials("Ama Mensah"), EmployeePhotoService.Initials("Kofi"), EmployeePhotoService.Initials("  ")));
    }

    [Fact]
    public async Task The_verified_onboarding_photograph_is_used_when_no_photo_was_uploaded()
    {
        await ActivatedAsync();
        var e = await EmployeeAsync();
        AsHrManager();
        var photos = () => Svc<IReadOnlySet<int>, EmployeePhotoService>(s => s.WithPhotoAsync([(e.Id, null, e.OnboardingRecordId)]));
        Assert.Empty(await photos()); // the test uploaded the passport photograph as a PDF: not a picture

        await Db(async db =>
        {
            var doc = await db.EmployeeDocuments.SingleAsync(d => d.OnboardingRecordId == e.OnboardingRecordId && d.DocumentTypeCode == EmployeePhotoService.OnboardingPhotoCode);
            db.Attachments.Add(new Attachment
            {
                OwnerType = AttachmentOwners.EmployeeDocument, OwnerId = doc.Id, Category = AttachmentCategory.NewHireDocument, OriginalFileName = "passport.jpg",
                StoredFileName = "x.jpg", ContentType = "image/jpeg", SizeBytes = 10, Sha256 = "x"
            });
            return await db.SaveChangesAsync();
        });
        Assert.Contains(e.Id, await photos());
    }

    [Fact]
    public async Task Completion_and_pending_actions_come_from_what_grsms_holds()
    {
        await ActivatedAsync();
        var e = await EmployeeAsync();
        AsHrManager();
        var x = (await Svc<EmployeeProfileExtras?, EmployeeService>(s => s.ProfileAsync(e.Id)))!;

        // Personal record, contacts, bank and documents were all completed through onboarding; no grade yet and no photo.
        var c = x.Completion;
        Assert.Equal(7, c.Items.Count);
        Assert.True(c.Items.Single(i => i.Section == "Personal information").Done, c.Items.Single(i => i.Section == "Personal information").Missing);
        Assert.True(c.Items.Single(i => i.Section == "Emergency contact").Done);
        Assert.True(c.Items.Single(i => i.Section == "Bank information").Done);
        Assert.True(c.Items.Single(i => i.Section == "Documents").Done);
        Assert.Equal("No grade", c.Items.Single(i => i.Section == "Employment details").Missing);
        Assert.Equal(["Employment details", "Photo"], c.Missing.Select(m => m.Section));
        Assert.Equal(71, c.Percent); // 5 of 7

        // The manager is known (the line manager from the onboarding, or the Head of Department); probation alerts follow its dates.
        Assert.NotNull(x.Team.Manager);
        Assert.NotNull(x.Probation);
        Assert.Contains(x.Alerts, a => a.Text.StartsWith("Employment details"));

        // A contract ending within 30 days is flagged first.
        await Db(async db => { (await db.Employees.SingleAsync(z => z.Id == e.Id)).ContractEndDate = Today.AddDays(10); return await db.SaveChangesAsync(); });
        x = (await Svc<EmployeeProfileExtras?, EmployeeService>(s => s.ProfileAsync(e.Id)))!;
        Assert.Equal(("danger", true), (x.Alerts[0].Severity, x.Alerts[0].Text.StartsWith("Contract expiring in 10 days")));

        // A member of staff registered directly has no personal record yet: those parts are missing, with the reason.
        var direct = await Db(async db =>
        {
            var emp = new Employee { EmployeeNumber = "GREL-0001", UserId = _staff, PositionTitle = "Clerk", DepartmentId = 3, SiteId = 1, HireDate = Today.AddYears(-3),
                Status = EmployeeStatus.Active, StatusChangedAt = DateTime.UtcNow };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            return emp.Id;
        });
        var d = (await Svc<EmployeeProfileExtras?, EmployeeService>(s => s.ProfileAsync(direct)))!.Completion;
        Assert.Contains("hired before GRSMS", d.Items.Single(i => i.Section == "Personal information").Missing);
        Assert.False(d.Items.Single(i => i.Section == "Documents").Done);
    }
}
