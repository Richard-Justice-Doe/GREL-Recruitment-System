using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// DEMO MODE ONLY. Fills an empty demo database with fictional people and records, so every screen has
/// something to show: one user per role, units, sites, and recruitment requests and vacancies at every
/// stage of their workflows.
///
/// Runs only when the environment is Development AND "Demo:SeedSampleData" is true (see DbInitializer),
/// and only once (it stops if the demo HOD account already exists). All names are made up.
/// Every demo account uses the password in "Demo:Password".
/// </summary>
public static class DemoDataSeeder
{
    private const int ApmDept = 31, ItDept = 2, HrDept = 1, StoresDept = 26; // Factory APM (Operations), IT and HR (Corporate)

    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync("recruiter@demo.grel") is not null) return; // already seeded (this account exists in every version of the demo data)

        var password = config["Demo:Password"] ?? "Demo#Pass2026";
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var numbers = sp.GetRequiredService<INumberGenerator>();

        // ---------------------------------------------------------------- people (one per role)
        async Task<ApplicationUser> User(string email, string name, int dept, params string[] roles)
        {
            var u = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = name,
                DepartmentId = dept, SiteId = 1, CreatedAt = DateTime.UtcNow
            };
            var created = await users.CreateAsync(u, password);
            if (!created.Succeeded) throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
            await users.AddToRolesAsync(u, roles);
            return u;
        }

        var hodApm = await User("hod.apm@demo.grel", "Kwame Asante", ApmDept, Roles.HeadOfDepartment, Roles.Employee);
        var hodIt = await User("hod.it@demo.grel", "Abena Owusu", ItDept, Roles.HeadOfDepartment, Roles.Employee);
        var recruiter = await User("recruiter@demo.grel", "Efua Mensah", HrDept, Roles.RecruitmentOfficer, Roles.Employee);
        var hrManager = await User("hr.manager@demo.grel", "Yaw Boateng", HrDept, Roles.HrManager, Roles.Employee);
        var md = await User("md@demo.grel", "Akosua Darko", HrDept, Roles.ManagingDirector, Roles.Employee);
        await User("employee@demo.grel", "Kofi Adjei", StoresDept, Roles.Employee);

        // ---------------------------------------------------------------- organisation
        (await db.Departments.FindAsync(ApmDept))!.HeadOfDepartmentUserId = hodApm.Id;
        (await db.Departments.FindAsync(ItDept))!.HeadOfDepartmentUserId = hodIt.Id;
        db.Units.AddRange(
            new Unit { DepartmentId = ApmDept, Name = "Fire & Emergency", Code = "FIRE" },
            new Unit { DepartmentId = ApmDept, Name = "Environment", Code = "ENV" },
            new Unit { DepartmentId = ItDept, Name = "Infrastructure", Code = "INFRA" },
            new Unit { DepartmentId = ItDept, Name = "Applications", Code = "APPS" });
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- recruitment requests at every stage
        var now = DateTime.UtcNow;

        async Task<RecruitmentRequest> Request(ApplicationUser hod, int dept, string code, string title, int positions,
            EmploymentType type, VacancyReason reason, decimal cost, int daysAgo)
        {
            var created = now.AddDays(-daysAgo);
            // The number generator saves its own change, so finish saving the previous request first.
            await db.SaveChangesAsync();
            var n = await numbers.NextAsync($"RR-{code}-{now.Year}");
            var r = new RecruitmentRequest
            {
                RequestNumber = $"RR-{code}-{now.Year}-{n:0000}",
                DivisionId = db.Departments.Find(dept)!.DivisionId,
                DepartmentId = dept, SiteId = 1, PositionTitle = title, NumberOfPositions = positions,
                EmploymentType = type, VacancyReason = reason,
                ReplacedEmployee = reason == VacancyReason.Replacement ? "J. Quaye (retired)" : null,
                Justification = $"The {title} role is needed to keep operations safe and on schedule. (Demo data.)",
                BudgetCode = $"{code}-OPS-{now.Year % 100}", EstimatedAnnualCost = cost,
                RequestedByUserId = hod.Id, Status = RecruitmentRequestStatus.Draft, StatusChangedAt = created
            };
            r.RecordStep(WorkflowAction.Created, RecruitmentRequestStatus.Draft, hod.Id, hod.FullName, null, created);
            db.RecruitmentRequests.Add(r);
            return r;
        }

        void Submit(RecruitmentRequest r, ApplicationUser hod, int daysAgo)
        {
            r.DateRequested = now.AddDays(-daysAgo);
            r.RecordStep(WorkflowAction.Submitted, RecruitmentRequestStatus.RecruitmentReview, hod.Id, hod.FullName, null, now.AddDays(-daysAgo));
        }

        void Approve(RecruitmentRequest r, ApplicationUser by, int daysAgo, string? comment = null)
        {
            if (r.Status == RecruitmentRequestStatus.RecruitmentReview) r.AssignedRecruiterUserId = recruiter.Id;
            if (r.Status == RecruitmentRequestStatus.HrApproval) { r.BudgetValidated = true; r.BudgetValidatedByUserId = hrManager.Id; }
            var next = RecruitmentWorkflow.NextAfterApproval(r.Status);
            if (next == RecruitmentRequestStatus.Approved) r.ApprovedAt = now.AddDays(-daysAgo);
            r.RecordStep(WorkflowAction.Approved, next, by.Id, by.FullName, comment, now.AddDays(-daysAgo));
        }

        // 1. Draft (HOD still preparing it)
        await Request(hodApm, ApmDept, "APM", "Safety Officer", 1, EmploymentType.Permanent, VacancyReason.NewPosition, 96000, 1);

        // 2. Waiting for the Recruitment Officer (4 days, so the "waiting" warning shows)
        var fireWarden = await Request(hodApm, ApmDept, "APM", "Fire Warden", 2, EmploymentType.FixedTermContract, VacancyReason.Expansion, 72000, 6);
        Submit(fireWarden, hodApm, 4);

        // 3. Waiting for the HR Manager
        var network = await Request(hodIt, ItDept, "IT", "Network Engineer", 1, EmploymentType.Permanent, VacancyReason.NewPosition, 140000, 8);
        Submit(network, hodIt, 7);
        Approve(network, recruiter, 2, "Role confirmed with IT. Recruiter assigned.");

        // 4. Waiting for the Managing Director
        var envOfficer = await Request(hodApm, ApmDept, "APM", "Environmental Officer", 1, EmploymentType.Permanent, VacancyReason.Replacement, 110000, 12);
        Submit(envOfficer, hodApm, 11);
        Approve(envOfficer, recruiter, 9);
        Approve(envOfficer, hrManager, 1, "Budget line checked with Finance.");

        // 5. Returned to the HOD for changes
        var helpDesk = await Request(hodIt, ItDept, "IT", "Help Desk Technician", 2, EmploymentType.Temporary, VacancyReason.TemporaryCover, 60000, 10);
        Submit(helpDesk, hodIt, 9);
        Approve(helpDesk, recruiter, 6);
        helpDesk.RecordStep(WorkflowAction.Returned, RecruitmentRequestStatus.Returned, hrManager.Id, hrManager.FullName,
            "Please reduce to one position and attach the updated organisational chart.", now.AddDays(-3));

        // 6. Approved, waiting for a vacancy to be created
        var firstAid = await Request(hodApm, ApmDept, "APM", "First Aid Coordinator", 1, EmploymentType.Permanent, VacancyReason.NewPosition, 80000, 20);
        Submit(firstAid, hodApm, 19);
        Approve(firstAid, recruiter, 15);
        Approve(firstAid, hrManager, 12);
        Approve(firstAid, md, 2, "Approved.");

        // 7 + 8. Approved requests that already have vacancies (one published, one awaiting HR approval)
        var sysAdmin = await Request(hodIt, ItDept, "IT", "Systems Administrator", 1, EmploymentType.Permanent, VacancyReason.Replacement, 150000, 30);
        Submit(sysAdmin, hodIt, 29);
        Approve(sysAdmin, recruiter, 25);
        Approve(sysAdmin, hrManager, 22);
        Approve(sysAdmin, md, 18);

        var inspector = await Request(hodApm, ApmDept, "APM", "Safety Inspector", 1, EmploymentType.FixedTermContract, VacancyReason.Expansion, 85000, 25);
        Submit(inspector, hodApm, 24);
        Approve(inspector, recruiter, 20);
        Approve(inspector, hrManager, 16);
        Approve(inspector, md, 10);

        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- vacancies
        var today = DateOnly.FromDateTime(now);

        async Task<Vacancy> Vacancy(RecruitmentRequest r, string description, EducationLevel education, string field, int years,
            string[] essentialSkills, string[] desirableSkills, string[] certifications, decimal min, decimal max, int closesInDays, int createdDaysAgo)
        {
            await db.SaveChangesAsync();
            var n = await numbers.NextAsync($"VAC-{now.Year}");
            var v = new Vacancy
            {
                VacancyNumber = $"VAC-{now.Year}-{n:0000}", RecruitmentRequestId = r.Id, JobTitle = r.PositionTitle,
                DivisionId = r.DivisionId, DepartmentId = r.DepartmentId, SiteId = r.SiteId,
                RequestingHodUserId = r.RequestedByUserId, ReportingManagerUserId = r.RequestedByUserId,
                EmploymentType = r.EmploymentType, NumberOfPositions = r.NumberOfPositions, VacancyType = VacancyType.InternalAndExternal,
                JobDescription = description, MinimumEducation = education, FieldOfStudy = field, MinYearsExperience = years,
                SalaryMin = min, SalaryMax = max, ShowSalary = true, ClosingDate = today.AddDays(closesInDays),
                RecruiterUserId = recruiter.Id, Status = VacancyStatus.Draft, StatusChangedAt = now.AddDays(-createdDaysAgo)
            };
            foreach (var s in essentialSkills) v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = s, IsEssential = true });
            foreach (var s in desirableSkills) v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = s, IsEssential = false });
            foreach (var c in certifications) v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Certification, Name = c, IsEssential = true });
            v.RecordStep(VacancyAction.Created, VacancyStatus.Draft, recruiter.Id, recruiter.FullName, $"Created from request {r.RequestNumber}", now.AddDays(-createdDaysAgo));
            v.RecordStep(VacancyAction.Submitted, VacancyStatus.PendingApproval, recruiter.Id, recruiter.FullName, null, now.AddDays(-createdDaysAgo));
            db.Vacancies.Add(v);
            return v;
        }

        var published = await Vacancy(sysAdmin,
            "Purpose: keep GREL's servers, accounts and backups secure and running.\n\nKey duties:\n- Administer Windows and Linux servers\n- Manage user accounts and access rights\n- Run and test backups\n- Patch systems and monitor security alerts\n\n(Demo data.)",
            EducationLevel.Bachelors, "Computer Science, IT or related", 3,
            ["Windows Server", "Linux administration", "Backup and recovery"], ["PowerShell scripting", "VMware"], ["CompTIA Security+"],
            9000, 12000, closesInDays: 12, createdDaysAgo: 16);
        published.PublishedAt = now.AddDays(-14);
        published.RecordStep(VacancyAction.Published, VacancyStatus.Published, hrManager.Id, hrManager.FullName, null, now.AddDays(-14));

        await Vacancy(inspector,
            "Purpose: inspect work sites and processing areas to make sure safety rules are followed.\n\nKey duties:\n- Carry out scheduled and surprise inspections\n- Record findings and follow up corrective actions\n- Support incident investigations\n\n(Demo data.)",
            EducationLevel.Diploma, "Occupational Health and Safety", 2,
            ["Risk assessment", "Incident investigation"], ["Report writing"], ["NEBOSH IGC"],
            5500, 7500, closesInDays: 20, createdDaysAgo: 2);

        await db.SaveChangesAsync();
        logger.LogWarning("DEMO MODE: sample users and records created. Demo accounts use the password from Demo:Password.");
    }
}
