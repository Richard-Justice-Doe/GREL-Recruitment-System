using System.Text;
using Grsms.Application.Employees;
using Grsms.Application.Probation;
using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Probation;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 28 (docs/30): the employee master record. Created when an onboarding is activated, made Active when the probation
/// is confirmed, backfilled for employees activated before Phase 28, and registered (form or CSV) for staff hired before
/// GRSMS. Job changes add history lines and never edit them.
/// </summary>
public class EmployeeServiceTests : OnboardingTestBase
{
    private async Task<T> RunEmployees<T>(Func<EmployeeService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<EmployeeService>());
    }

    private async Task<T> RunGrades<T>(Func<GradeService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<GradeService>());
    }

    private async Task<T> RunProbation<T>(Func<ProbationService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ProbationService>());
    }

    private Task<Employee> EmployeeAsync(string userId) => Db(db => db.Employees.SingleAsync(e => e.UserId == userId));

    private Task<List<EmployeeAssignment>> HistoryAsync(int employeeId) =>
        Db(db => db.EmployeeAssignments.Where(a => a.EmployeeId == employeeId).OrderBy(a => a.Id).ToListAsync());

    private RegisterEmployeeForm Kwame() => new()
    {
        FullName = "Kwame  Boateng", Email = "k.boateng@grel.test", Phone = "0244000001", EmployeeNumber = "grel-0101", PositionTitle = "Field Supervisor",
        DepartmentId = Apm, SiteId = 2, EmploymentType = EmploymentType.Permanent, HireDate = Today.AddYears(-6), Status = EmployeeStatus.Active,
        ConfirmationDate = Today.AddYears(-6).AddMonths(6)
    };

    // ------------------------------------------------------------------ from recruitment

    [Fact]
    public async Task Activating_an_onboarding_creates_the_employee_record_and_files_the_offer()
    {
        await ActivatedAsync();

        var e = await EmployeeAsync(_candidate);
        Assert.Equal(("GREL-0457", "Process Engineer", Apm, 2, _hod, EmployeeStatus.OnProbation, Today, EmploymentType.Permanent),
            (e.EmployeeNumber, e.PositionTitle, e.DepartmentId, e.SiteId, e.ReportsToUserId, e.Status, e.HireDate, e.EmploymentType));
        Assert.NotNull(e.OnboardingRecordId);
        var line = Assert.Single(await HistoryAsync(e.Id));
        Assert.Equal((AssignmentSource.Hire, Today, (DateOnly?)null, e.OnboardingRecordId), (line.Source, line.EffectiveFrom, line.EffectiveTo, line.SourceId));

        // The accepted offer is now in the personnel file too (Recruitment documents), frozen as accepted.
        var offer = await Db(db => db.PersonnelFileDocuments.SingleAsync(d => d.SourceType == "OfferLetter"));
        Assert.Equal(PersonnelFileCategories.RecruitmentDocuments, offer.CategoryId);
        Assert.Contains("8,500.00", offer.Content);

        // HR sees the record; the employee page links the onboarding, personnel file and probation.
        AsHrManager();
        var details = (await RunEmployees(s => s.GetAsync(e.Id)))!;
        Assert.Equal(("ama", "Process Engineer"), (details.Name, details.PositionTitle));
        Assert.NotNull(details.PersonnelFileId);
        Assert.Equal(ProbationStatus.InitialMeeting, details.ProbationStatus);
        Assert.True(details.CanManage);
    }

    [Fact]
    public async Task Confirming_the_probation_makes_the_employee_active()
    {
        await ActivatedAsync();
        var p = await Db(db => db.ProbationRecords.SingleAsync());
        As(_hod, Roles.HeadOfDepartment);
        Assert.True((await RunProbation(s => s.RecordInitialMeetingAsync(p.Id, new InitialMeetingForm
        {
            MeetingDate = Today, SignatureName = _hod,
            Objectives = [new() { Objective = "Run the line safely", SuccessIndicators = "No incidents", DueDate = Today.AddMonths(3) }],
            DevelopmentPlan = [new() { TrainingNeed = "Lock-out / tag-out", Timeline = "Month 1" }]
        }))).Succeeded);
        As(_candidate, Roles.Employee);
        Assert.True((await RunProbation(s => s.SignAsEmployeeAsync(p.Id, "Ama Mensah"))).Succeeded);
        await Db(async db =>
        {
            var r = await db.ProbationRecords.SingleAsync();
            var shift = r.EndDate.DayNumber - Today.DayNumber;
            (r.StartDate, r.EndDate, r.OriginalEndDate) = (r.StartDate.AddDays(-shift), Today, r.OriginalEndDate.AddDays(-shift));
            return await db.SaveChangesAsync();
        });
        AssessmentForm Ratings(ProbationOutcome? outcome) => new()
        {
            ApplicationOfSkills = ProbationRating.Good, QualityOfWork = ProbationRating.Good, Productivity = ProbationRating.Good,
            Cooperation = ProbationRating.Good, Dependability = ProbationRating.Good, Comments = "Settled in well; reliable on shift.", Outcome = outcome
        };
        As(_hod, Roles.HeadOfDepartment);
        var first = await RunProbation(s => s.AssessAsync(p.Id, Ratings(null)));
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.True((await RunProbation(s => s.AssessAsync(p.Id, Ratings(ProbationOutcome.Confirm)))).Succeeded);
        Assert.Equal(EmployeeStatus.OnProbation, (await EmployeeAsync(_candidate)).Status);

        AsHrManager();
        Assert.True((await RunProbation(s => s.HrDecideAsync(p.Id, true, "Well done."))).Succeeded);
        var e = await EmployeeAsync(_candidate);
        Assert.Equal((EmployeeStatus.Active, (DateOnly?)Today), (e.Status, e.ConfirmationDate));
    }

    [Fact]
    public async Task Employees_activated_before_phase_28_get_their_record_at_start_up_once()
    {
        await ActivatedAsync();
        // As if activated before Phase 28: no employee record, no offer in the personnel file.
        await Db(async db =>
        {
            await db.EmployeeAssignments.ExecuteDeleteAsync();
            await db.Employees.ExecuteDeleteAsync();
            return await db.PersonnelFileDocuments.Where(d => d.SourceType == "OfferLetter").ExecuteDeleteAsync();
        });
        var before = await Db(db => db.PersonnelFileDocuments.CountAsync());

        using (var scope = _db.NewScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<EmployeeLifecycle>().BackfillAsync());
        using (var scope = _db.NewScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<EmployeeLifecycle>().BackfillAsync()); // safe to run again

        var e = await EmployeeAsync(_candidate);
        Assert.Equal(("GREL-0457", EmployeeStatus.OnProbation), (e.EmployeeNumber, e.Status));
        Assert.Equal(AssignmentSource.Hire, Assert.Single(await HistoryAsync(e.Id)).Source);
        Assert.Equal(before + 1, await Db(db => db.PersonnelFileDocuments.CountAsync())); // the offer filed; nothing filed twice
    }

    // ------------------------------------------------------------------ staff hired before GRSMS

    [Fact]
    public async Task Hr_registers_an_existing_employee_with_a_new_staff_account()
    {
        AsRecruiter();
        Assert.False((await RunEmployees(s => s.RegisterAsync(Kwame()))).Succeeded); // may view, not register

        AsHrManager();
        var wrong = Kwame();
        (wrong.Email, wrong.HireDate) = ("ama@mail.test", Today.AddDays(3));
        var errors = (await RunEmployees(s => s.RegisterAsync(wrong))).Errors;
        Assert.True(errors.Any(e => e.Contains("candidate account")), string.Join(" | ", errors));
        Assert.Contains(errors, e => e.Contains("cannot be in the future"));

        var id = (await RunEmployees(s => s.RegisterAsync(Kwame()))).Value;
        var e = await Db(db => db.Employees.SingleAsync(x => x.Id == id));
        Assert.Equal(("GREL-0101", "Field Supervisor", EmployeeStatus.Active, (int?)null), (e.EmployeeNumber, e.PositionTitle, e.Status, e.OnboardingRecordId));
        Assert.Equal(AssignmentSource.Registration, Assert.Single(await HistoryAsync(id)).Source);

        using (var scope = _db.NewScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByEmailAsync("k.boateng@grel.test"))!;
            Assert.Equal(("Kwame Boateng", "GREL-0101", (int?)Apm, false), (account.FullName, account.StaffNumber, account.DepartmentId, account.IsCandidate));
            Assert.Equal([Roles.Employee], await users.GetRolesAsync(account));
            Assert.False(await users.HasPasswordAsync(account)); // they set one with "Forgot password"
        }

        var again = Kwame();
        again.Email = "other@grel.test";
        var againErrors = (await RunEmployees(s => s.RegisterAsync(again))).Errors;
        Assert.True(againErrors.Any(x => x.Contains("already registered")), string.Join(" | ", againErrors));
    }

    [Fact]
    public async Task Registering_an_existing_staff_account_uses_it()
    {
        AsHrManager();
        var form = Kwame();
        (form.Email, form.FullName, form.DepartmentId) = ("clerk@grel.test", "Clerk Person", 3);
        var id = (await RunEmployees(s => s.RegisterAsync(form))).Value;
        Assert.Equal(_staff, (await Db(db => db.Employees.SingleAsync(x => x.Id == id))).UserId);
        Assert.Contains((await RunEmployees(s => s.RegisterAsync(form))).Errors, x => x.Contains("already registered"));
    }

    [Fact]
    public async Task Updating_job_details_adds_to_the_history_and_never_edits_it()
    {
        AsHrManager();
        var gradeId = (await RunGrades(s => s.CreateAsync(new GradeFormDto { Code = "g5", Name = "Senior Officer", SortOrder = 5 }))).Value;
        var id = (await RunEmployees(s => s.RegisterAsync(Kwame()))).Value;

        var form = (await RunEmployees(s => s.GetUpdateFormAsync(id)))!;
        Assert.Contains((await RunEmployees(s => s.UpdateJobAsync(form))).Errors, e => e.Contains("Say why"));
        form.Note = "Grade given after the 2026 grading exercise.";
        Assert.Equal("Nothing changed.", Assert.Single((await RunEmployees(s => s.UpdateJobAsync(form))).Errors));

        (form.GradeId, form.ReportsToUserId, form.DepartmentId, form.SiteId) = (gradeId, _hod, It, 1);
        var updated = await RunEmployees(s => s.UpdateJobAsync(form));
        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors));
        Assert.Contains((await RunEmployees(s => s.UpdateJobAsync(form))).Errors, e => e.Contains("Someone else")); // the old version

        var history = await HistoryAsync(id);
        Assert.Equal(2, history.Count);
        Assert.Equal(("Field Supervisor", Apm, (int?)null, (DateOnly?)Today.AddDays(-1)), (history[0].PositionTitle, history[0].DepartmentId, history[0].GradeId, history[0].EffectiveTo));
        Assert.Equal((AssignmentSource.Update, It, (int?)gradeId, (DateOnly?)null, _hod), (history[1].Source, history[1].DepartmentId, history[1].GradeId, history[1].EffectiveTo, history[1].ReportsToUserId));
        var e = await Db(db => db.Employees.SingleAsync(x => x.Id == id));
        Assert.Equal((It, (int?)gradeId), (e.DepartmentId, e.GradeId));
        // The account moved with them, so the IT Head of Department now sees them and the APM one no longer does.
        Assert.Equal(It, await Db(db => db.Users.Where(u => u.Id == e.UserId).Select(u => u.DepartmentId).SingleAsync()));
        As(_itHod, Roles.HeadOfDepartment);
        Assert.NotNull(await RunEmployees(s => s.GetAsync(id)));
        Assert.False((await RunEmployees(s => s.GetAsync(id)))!.CanManage);
        As(_hod, Roles.HeadOfDepartment);
        Assert.Null(await RunEmployees(s => s.GetAsync(id)));
        Assert.Equal(1, (await RunGrades(s => s.GetAllAsync())).Single().Employees);
    }

    [Fact]
    public async Task Hr_cannot_change_their_own_job_details()
    {
        AsHrManager();
        var form = Kwame();
        (form.Email, form.FullName, form.EmployeeNumber) = ("hrm@grel.test", "HR Manager", "GREL-0001");
        var id = (await RunEmployees(s => s.RegisterAsync(form))).Value;
        Assert.Null(await RunEmployees(s => s.GetUpdateFormAsync(id)));
        var update = new UpdateJobForm { EmployeeId = id, EffectiveFrom = Today, PositionTitle = "HR Director", DepartmentId = 1, SiteId = 1, Note = "Promoted myself." };
        Assert.Contains((await RunEmployees(s => s.UpdateJobAsync(update))).Errors, e => e.Contains("your own"));
    }

    // ------------------------------------------------------------------ CSV import

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task A_csv_with_a_wrong_row_imports_nothing_and_says_which_row()
    {
        AsHrManager();
        var csv = "StaffNumber,FullName,Email,Position,Department,Site,HireDate,Grade\r\n" +
                  "GREL-0201,Efua Asante,e.asante@grel.test,Clerk,APM,HQ,2018-05-02,\r\n" +
                  "GREL-0202,Yaw Mensah,y.mensah@grel.test,Driver,No Such Department,HQ,02/05/2018,\r\n" +
                  "GREL-0201,Abena Ofori,a.ofori@grel.test,Driver,APM,HQ,31/02/2018,X9\r\n";
        var result = await RunEmployees(s => s.ImportAsync(Csv(csv)));
        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("Row 3 (Yaw Mensah)", result.Errors[0]);
        Assert.Contains("no active department 'No Such Department'", result.Errors[0]);
        Assert.Contains("Row 4", result.Errors[1]);
        Assert.Contains("not a date", result.Errors[1]);
        Assert.Contains("no active grade 'X9'", result.Errors[1]);
        Assert.Contains("appears twice", result.Errors[1]);
        Assert.Equal(0, await Db(db => db.Employees.CountAsync()));

        Assert.Contains("missing: Site", Assert.Single((await RunEmployees(s => s.ImportAsync(Csv("StaffNumber,FullName,Email,Position,Department,HireDate\r\nA,B,c@d.e,P,APM,2020-01-01")))).Errors));
    }

    [Fact]
    public async Task A_valid_csv_imports_everyone_with_line_managers_from_the_file()
    {
        AsHrManager();
        var apm = await Db(db => db.Departments.Where(d => d.Id == Apm).Select(d => d.Name).SingleAsync());
        var csv = "Staff Number,Full Name,Email,Phone,Position,Department,Site,Grade,Employment Type,Hire Date,Status,Confirmation Date,Reports To Email,Contract End Date\n" +
                  $"GREL-0301,Akosua Darko,a.darko@grel.test,,Field Officer,\"{apm}\",HQ,,Contract,2023-01-09,On probation,,k.owusu@grel.test,2027-01-08\n" +
                  $"GREL-0302,\"Kofi Owusu, Jr\",k.owusu@grel.test,0244000002,Estate Manager,{apm},HQ,,,2015-07-01,Active,2016-01-01,hod@grel.test,\n";
        var result = await RunEmployees(s => s.ImportAsync(Csv(csv)));
        Assert.True(result.Succeeded, string.Join(" | ", result.Errors));
        Assert.Equal(2, result.Imported);

        var akosua = await Db(db => db.Employees.SingleAsync(e => e.EmployeeNumber == "GREL-0301"));
        var kofi = await Db(db => db.Employees.SingleAsync(e => e.EmployeeNumber == "GREL-0302"));
        Assert.Equal((EmploymentType.FixedTermContract, EmployeeStatus.OnProbation, (DateOnly?)new DateOnly(2027, 1, 8), kofi.UserId),
            (akosua.EmploymentType, akosua.Status, akosua.ContractEndDate, akosua.ReportsToUserId));
        Assert.Equal(kofi.UserId, Assert.Single(await HistoryAsync(akosua.Id)).ReportsToUserId);
        Assert.Equal((_hod, EmployeeStatus.Active), (kofi.ReportsToUserId, kofi.Status));
        Assert.Equal("Kofi Owusu, Jr", await Db(db => db.Users.Where(u => u.Id == kofi.UserId).Select(u => u.FullName).SingleAsync()));

        // The list: HR sees both; the search finds by name.
        Assert.Equal(2, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter()))).TotalCount);
        Assert.Equal("GREL-0302", Assert.Single((await RunEmployees(s => s.SearchAsync(new EmployeeFilter { Search = "owusu" }))).Items).EmployeeNumber);
        Assert.Equal(new EmployeeCountsDto(2, 1, 1, 0), await RunEmployees(s => s.CountsAsync()));
    }

    [Fact]
    public async Task The_list_is_paged_and_searched_in_the_database()
    {
        AsHrManager();
        var csv = new StringBuilder("StaffNumber,FullName,Email,Position,Department,Site,HireDate\n");
        for (var i = 1; i <= 12; i++)
            csv.Append($"GREL-1{i:000},Worker {i:00},w{i}@grel.test,{(i == 7 ? "Tapper_Lead" : "Tapper")},{(i <= 9 ? "APM" : "HR")},ABU,2020-01-01\n");
        Assert.True((await RunEmployees(s => s.ImportAsync(Csv(csv.ToString())))).Succeeded);

        var first = await RunEmployees(s => s.SearchAsync(new EmployeeFilter { PageSize = 10 }));
        Assert.Equal((12, 2, 10), (first.TotalCount, first.TotalPages, first.Items.Count));
        Assert.Equal("Worker 01", first.Items[0].Name); // sorted by name
        var second = await RunEmployees(s => s.SearchAsync(new EmployeeFilter { PageSize = 10, Page = 2 }));
        Assert.Equal(["Worker 11", "Worker 12"], second.Items.Select(e => e.Name));
        Assert.Equal(2, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter { PageSize = 10, Page = 99 }))).Page); // past the end: the last page

        // Search: name, staff number or position, case-insensitive; "_" is a plain character, not a wildcard.
        Assert.Equal(1, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter { Search = "WORKER 03" }))).TotalCount);
        Assert.Equal(1, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter { Search = "grel-1012" }))).TotalCount);
        Assert.Equal("Worker 07", Assert.Single((await RunEmployees(s => s.SearchAsync(new EmployeeFilter { Search = "r_l" }))).Items).Name);

        // The APM Head of Department sees the 9 APM employees only, in the list and in the counts.
        As(_hod, Roles.HeadOfDepartment);
        Assert.Equal(9, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter()))).TotalCount);
        Assert.Equal(new EmployeeCountsDto(9, 0, 9, 0), await RunEmployees(s => s.CountsAsync()));
        As(_staff, Roles.Employee);
        Assert.Equal(0, (await RunEmployees(s => s.SearchAsync(new EmployeeFilter()))).TotalCount); // no permission
    }

    [Fact]
    public async Task Contract_expiry_alerts_go_out_once_per_stage()
    {
        AsHrManager();
        var form = Kwame();
        (form.EmploymentType, form.ContractEndDate) = (EmploymentType.FixedTermContract, Today.AddDays(45));
        Assert.True((await RunEmployees(s => s.RegisterAsync(form))).Succeeded);
        var permanent = Kwame();
        (permanent.Email, permanent.EmployeeNumber) = ("p.permanent@grel.test", "GREL-0102"); // no contract end: never alerted
        Assert.True((await RunEmployees(s => s.RegisterAsync(permanent))).Succeeded);

        As(_staff, Roles.Employee); // the daily job acts as nobody in particular (the mailer never emails the person acting)
        async Task<int> AlertsOn(DateOnly day)
        {
            using var scope = _db.NewScope();
            return await scope.ServiceProvider.GetRequiredService<ContractAlertService>().RunAsync(day);
        }

        Assert.Equal(1, await AlertsOn(Today));         // 45 days left: the 60-day alert
        Assert.Equal(0, await AlertsOn(Today));         // not twice
        Assert.Equal(0, await AlertsOn(Today.AddDays(10))); // 35 days left: still the 60-day stage
        Assert.Equal(1, await AlertsOn(Today.AddDays(15))); // 30 days left
        Assert.Equal(1, await AlertsOn(Today.AddDays(40))); // 5 days left (a missed day): the 7-day alert
        Assert.Equal(0, await AlertsOn(Today.AddDays(46))); // ended

        var mails = _db.SentEmails();
        Assert.Contains(mails, m => m.Contains("To: hrm@grel.test") && m.Contains("Contract ending in 45 days"));
        Assert.Contains(mails, m => m.Contains("To: hod@grel.test") && m.Contains("Contract ending in 45 days")); // Head of APM
        Assert.Contains(mails, m => m.Contains("To: k.boateng@grel.test") && m.Contains("Your contract ends"));
    }

    [Fact]
    public void The_csv_reader_handles_quotes_commas_and_line_ends()
    {
        var rows = EmployeeService.ParseCsv("a,\"b, c\",\"say \"\"hi\"\"\"\r\n\"multi\nline\",x\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b, c", "say \"hi\""], rows[0]);
        Assert.Equal(["multi\nline", "x"], rows[1]);
    }
}
