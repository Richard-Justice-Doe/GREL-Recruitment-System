using Grsms.Application.Onboarding;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 20: departmental induction (the 12 areas, their owners and tasks, HR verification of tasks done by others) and the
/// electronic personnel file (opened at activation, documents filed in the right folders once only, letters, HR uploads,
/// who may open it).
/// </summary>
public class InductionAndPersonnelFileTests : OnboardingTestBase
{
    private async Task<T> RunAreas<T>(Func<InductionAreaService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<InductionAreaService>());
    }

    private async Task<T> RunFiles<T>(Func<PersonnelFileService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<PersonnelFileService>());
    }

    // ------------------------------------------------------------------ induction

    [Fact]
    public async Task Activation_adds_the_chosen_areas_tasks_to_their_owners_once()
    {
        var security = await _db.CreateUserAsync("security@grel.test", 1, Roles.Employee);
        As("admin", Roles.HrAdministrator);
        Assert.True((await RunAreas(s => s.SaveAsync(InductionArea.Security, security, true, "Access card\nSecurity induction\nGate pass"))).Succeeded);

        var id = await ReadyAsync();
        AsHrManager();
        var choices = (await Run(s => s.GetAsync(id)))!.InductionChoices;
        Assert.Equal(12, choices.Count);
        Assert.True(choices.Single(c => c.Area == InductionArea.Security).Ticked);
        Assert.False(choices.Single(c => c.Area == InductionArea.TbuOperations).Ticked);

        AsRecruiter(); // Phase 27: the areas are ticked when activating
        Assert.True((await Run(s => s.ActivateAsync(id, "GREL-0457", areas: [InductionArea.Security, InductionArea.TbuOperations, InductionArea.CorporateIt]))).Succeeded);
        var list = await TasksAsync(id);
        var sec = list.Where(t => t.Area == InductionArea.Security).ToList();
        Assert.Equal(["Access card", "Security induction", "Gate pass"], sec.Select(t => t.Title));
        Assert.All(sec, t => Assert.Equal(security, t.AssignedToUserId));
        Assert.All(sec, t => Assert.Equal(Today.AddDays(OnboardingWorkflow.InductionTaskDueDays), t.DueDate));
        Assert.All(sec, t => Assert.NotNull(t.AssignedAt));
        Assert.Equal(_ro, list.Single(t => t.Area == InductionArea.TbuOperations).AssignedToUserId); // no owner: HR
        Assert.Equal(_itHod, list.First(t => t.Title == "Email setup").AssignedToUserId);           // IT falls back to the Head of IT
        Assert.DoesNotContain(list, t => t.Area == InductionArea.HealthAndSafety);                  // not ticked
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: security@grel.test") && m.Contains("induction can begin"));
        Assert.Contains("Induction areas: Corporate Information Technology, TBU Operations, Security", (await Run(s => s.GetAsync(id)))!.History.Last().Comments);
    }

    [Fact]
    public async Task Tasks_done_outside_hr_need_hr_verification_before_the_induction_closes()
    {
        var id = await ReadyAsync();
        AsHrManager();
        Assert.True((await Run(s => s.ActivateAsync(id, "GREL-0457", areas: []))).Succeeded);

        var deptTask = (await TasksAsync(id)).First(t => t.Team == OnboardingTeam.Department);
        As(_hod, Roles.HeadOfDepartment);
        Assert.True((await Run(s => s.UpdateTaskAsync(deptTask.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.Null((await TasksAsync(id)).Single(t => t.Id == deptTask.Id).VerifiedAt);
        Assert.False((await Run(s => s.VerifyTaskAsync(deptTask.Id))).Succeeded); // HR only

        AsRecruiter();
        foreach (var t in (await TasksAsync(id)).Where(t => t.Status == OnboardingTaskStatus.Open))
            Assert.True((await Run(s => s.UpdateTaskAsync(t.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.Contains("verify", (await Run(s => s.GetAsync(id)))!.InductionBlockedReason);
        Assert.True((await Run(s => s.GetAsync(id)))!.Tasks.Single(t => t.Id == deptTask.Id).CanVerify);
        Assert.True((await Run(s => s.VerifyTaskAsync(deptTask.Id))).Succeeded);
        Assert.Equal(_ro, (await TasksAsync(id)).Single(t => t.Id == deptTask.Id).VerifiedByUserId);
        Assert.True((await Run(s => s.CompleteInductionAsync(id))).Succeeded);
    }

    [Fact]
    public async Task Only_organisation_data_managers_change_induction_areas_and_input_is_checked()
    {
        AsRecruiter();
        Assert.False((await RunAreas(s => s.SaveAsync(InductionArea.Engineering, null, true, "Workshop tour"))).Succeeded);

        As("admin", Roles.HrAdministrator);
        Assert.False((await RunAreas(s => s.SaveAsync(InductionArea.Engineering, null, true, "  \n "))).Succeeded);       // no task
        Assert.False((await RunAreas(s => s.SaveAsync(InductionArea.Engineering, "no-such-user", true, "Workshop tour"))).Succeeded); // not an active staff member
        Assert.True((await RunAreas(s => s.SaveAsync(InductionArea.Engineering, _staff, true, "Workshop tour\nPermit-to-work"))).Succeeded);

        var row = (await RunAreas(s => s.GetAllAsync())).Single(r => r.Area == InductionArea.Engineering);
        Assert.Equal((_staff, true, "Workshop tour\nPermit-to-work", false), (row.OwnerUserId, row.IsDefault, row.Tasks, row.TasksAreBuiltIn));
        Assert.True((await RunAreas(s => s.GetAllAsync())).Single(r => r.Area == InductionArea.Security).TasksAreBuiltIn);
    }

    // ------------------------------------------------------------------ personnel file

    [Fact]
    public async Task Activation_opens_the_personnel_file_with_everything_in_the_right_folders()
    {
        var id = await ActivatedAsync();
        AsRecruiter();
        var fileId = (await Run(s => s.GetAsync(id)))!.PersonnelFileId!.Value;
        var file = (await RunFiles(s => s.GetAsync(fileId)))!;
        Assert.Matches(@"^PF-\d{4}-0001$", file.FileNumber);
        Assert.Equal(("Ama Mensah", "GREL-0457"), (file.EmployeeName, file.StaffNumber));
        Assert.Equal(["Recruitment Documents", "Employee Records", "Identification Records", "Compliance Records", "Onboarding Records",
                      "Probation Records", "Generated Letters"], file.Folders.Select(f => f.Name));

        IReadOnlyList<string> In(int category) => file.Folders.Single(f => f.CategoryId == category).Documents.Select(d => d.Title).ToList();
        Assert.Contains(In(PersonnelFileCategories.IdentificationRecords), t => t.StartsWith("Ghana Card"));
        Assert.Contains(In(PersonnelFileCategories.ComplianceRecords), t => t.StartsWith("Police clearance"));
        Assert.Contains(In(PersonnelFileCategories.RecruitmentDocuments), t => t.StartsWith("Updated CV"));
        Assert.Contains("Employee Personal Record Declaration (signed)", In(PersonnelFileCategories.EmployeeRecords));
        Assert.Contains(In(PersonnelFileCategories.GeneratedLetters), t => t.StartsWith("Letter of appointment APT-"));
        Assert.Contains(In(PersonnelFileCategories.OnboardingRecords), t => t.StartsWith("Onboarding and induction record"));
        Assert.Empty(In(PersonnelFileCategories.ProbationRecords));
        Assert.Equal(11, file.Folders.Sum(f => f.Documents.Count(d => d.Kind == PersonnelDocumentKind.File))); // the 11 verified uploads; "not applicable" ones have no file

        // The appointment letter is frozen as signed, and shown cleaned.
        var letterId = file.Folders.Single(f => f.CategoryId == PersonnelFileCategories.GeneratedLetters).Documents.Single().Id;
        var letter = (await RunFiles(s => s.GetLetterAsync(letterId)))!;
        Assert.Contains("Accepted and signed electronically</strong> by Ama Mensah", letter.Html);
        Assert.Contains("Probation: 6 months", letter.Html);

        // Opening again files nothing twice.
        var before = await Db(db => db.PersonnelFileDocuments.CountAsync());
        using (var scope = _db.NewScope())
            await scope.ServiceProvider.GetRequiredService<PersonnelFileService>().OpenForOnboardingAsync(await RecordAsync(id));
        Assert.Equal(before, await Db(db => db.PersonnelFileDocuments.CountAsync()));
        Assert.Equal(1, await Db(db => db.PersonnelFiles.CountAsync()));
    }

    [Fact]
    public async Task An_onboarding_completed_before_personnel_files_existed_gets_its_file_on_request()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        await IssueAndSignAsync(id, app);
        await Db(async db =>
        {
            var o = await db.Onboardings.SingleAsync(x => x.Id == id);
            (o.Status, o.EmployeeNumber) = (OnboardingStatus.Completed, "GREL-0100"); // as an older, already completed onboarding
            await db.PersonnelFiles.Where(f => f.EmployeeUserId == _candidate).ExecuteDeleteAsync(); // Phase 27 opens one at start; older ones had none
            return await db.SaveChangesAsync();
        });

        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.OpenPersonnelFileAsync(id))).Succeeded);
        AsRecruiter();
        Assert.Null((await Run(s => s.GetAsync(id)))!.PersonnelFileId);
        var opened = await Run(s => s.OpenPersonnelFileAsync(id));
        Assert.True(opened.Succeeded, string.Join(" ", opened.Errors));
        Assert.Equal(opened.Value, (await Run(s => s.GetAsync(id)))!.PersonnelFileId);
        var file = (await RunFiles(s => s.GetAsync(opened.Value)))!;
        Assert.Contains(file.Folders.SelectMany(f => f.Documents), d => d.Title.StartsWith("Letter of appointment"));
    }

    [Fact]
    public async Task Only_holders_of_the_permission_open_personnel_files_and_hr_can_add_documents()
    {
        var id = await ActivatedAsync();
        var fileId = await Db(db => db.PersonnelFiles.Select(f => f.Id).SingleAsync());

        foreach (var (user, role) in new[] { (_hod, Roles.HeadOfDepartment), (_candidate, Roles.Employee), (_staff, Roles.Employee) })
        {
            As(user, role);
            Assert.Null(await RunFiles(s => s.GetAsync(fileId)));
            Assert.Empty(await RunFiles(s => s.SearchAsync(null, null)));
            Assert.False((await RunFiles(s => s.UploadAsync(fileId, PersonnelFileCategories.ComplianceRecords, null, "x.pdf", FakePdf()))).Succeeded);
        }

        AsHrManager();
        Assert.Single(await RunFiles(s => s.SearchAsync("0457", null)));
        Assert.False((await RunFiles(s => s.UploadAsync(fileId, 99, null, "x.pdf", FakePdf()))).Succeeded); // no such folder
        var added = await RunFiles(s => s.UploadAsync(fileId, PersonnelFileCategories.ComplianceRecords, "Signed code of conduct", "conduct.pdf", FakePdf()));
        Assert.True(added.Succeeded, string.Join(" ", added.Errors));
        var doc = (await RunFiles(s => s.GetAsync(fileId)))!.Folders.Single(f => f.CategoryId == PersonnelFileCategories.ComplianceRecords)
            .Documents.Single(d => d.Title == "Signed code of conduct");
        Assert.Equal(("conduct.pdf", "hrm"), (doc.FileName, doc.AddedBy));
        await using var download = (await RunFiles(s => s.OpenAsync(doc.Id)))!.Content;
        Assert.Equal('%', (char)download.ReadByte());
    }
}
