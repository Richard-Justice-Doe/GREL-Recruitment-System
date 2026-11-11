using System.Globalization;
using System.Text;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;

namespace Grsms.Application.Employees;

/// <summary>
/// Phase 28 (docs/30): the employee master record.
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * See the list and a record : "Open employees". Without "See all departments" only the departments the person heads.
///   * Register, import, update  : "Manage employees" (HR Manager and HR Administrator by default), never on their own record.
/// Updating job details never edits history: it closes the current job history line and adds a new one. A change of
/// department or site also moves the user account, as activation does, so what a Head of Department sees follows the
/// employee. Recruitment records keep their own department.
/// </summary>
public class EmployeeService(
    IRepository<Employee> employees,
    IRepository<EmployeeAssignment> assignments,
    IRepository<Department> departments,
    IRepository<Division> divisions,
    IRepository<Site> sites,
    IRepository<Grade> grades,
    IRepository<OnboardingRecord> onboardings,
    IRepository<PersonnelFile> personnelFiles,
    IRepository<ProbationRecord> probations,
    IEmployeeAccounts accounts,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope)
{
    private const string ConflictMessage = "Someone else updated this employee at the same moment. Please reload the page and try again.";
    public const int MaxImportRows = 2000;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public bool MayView => me.HasPermission(Permissions.ViewEmployees);
    public bool MayManage => me.HasPermission(Permissions.ManageEmployees);

    // ===================================================================== Reading

    private async Task<Func<Employee, bool>> VisibleAsync(CancellationToken ct)
    {
        if (!MayView) return _ => false;
        var v = await scope.VisibilityAsync(ct);
        return e => v.SeeAll || v.HodDepartmentIds.Contains(e.DepartmentId) || e.UserId == v.UserId;
    }

    public async Task<IReadOnlyList<EmployeeListItemDto>> SearchAsync(EmployeeFilter filter, CancellationToken ct = default)
    {
        var visible = await VisibleAsync(ct);
        IEnumerable<Employee> q = (await employees.ListAsync(ct: ct)).Where(visible);
        if (filter.DepartmentId is { } dep) q = q.Where(e => e.DepartmentId == dep);
        if (filter.Status is { } status) q = q.Where(e => e.Status == status);
        else q = q.Where(e => e.Status != EmployeeStatus.Separated);
        if (filter.GradeId is { } grade) q = q.Where(e => e.GradeId == grade);
        var list = q.ToList();
        var names = await users.GetDisplayNamesAsync(list.Select(e => e.UserId).Distinct(), ct);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            list = list.Where(e => names.GetValueOrDefault(e.UserId, "").Contains(s, StringComparison.OrdinalIgnoreCase)
                                   || e.EmployeeNumber.Contains(s, StringComparison.OrdinalIgnoreCase)
                                   || e.PositionTitle.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var (deptNames, siteNames, gradeNames) = await NamesAsync(ct);
        return list.OrderBy(e => names.GetValueOrDefault(e.UserId, ""))
            .Select(e => new EmployeeListItemDto(e.Id, e.EmployeeNumber, names.GetValueOrDefault(e.UserId, e.EmployeeNumber), e.PositionTitle,
                deptNames.GetValueOrDefault(e.DepartmentId, ""), siteNames.GetValueOrDefault(e.SiteId, ""),
                e.GradeId is { } g ? gradeNames.GetValueOrDefault(g) : null, e.EmploymentType, e.Status, e.HireDate))
            .ToList();
    }

    /// <summary>Counts for the list's tabs (what the person may see).</summary>
    public async Task<EmployeeCountsDto> CountsAsync(CancellationToken ct = default)
    {
        var visible = await VisibleAsync(ct);
        var list = (await employees.ListAsync(ct: ct)).Where(visible).ToList();
        return new EmployeeCountsDto(list.Count(e => e.Status != EmployeeStatus.Separated), list.Count(e => e.Status == EmployeeStatus.OnProbation),
            list.Count(e => e.Status == EmployeeStatus.Active), list.Count(e => e.Status == EmployeeStatus.Separated));
    }

    public async Task<EmployeeDetailsDto?> GetAsync(int id, CancellationToken ct = default)
    {
        if (await employees.GetByIdAsync(id, ct) is not { } e || !(await VisibleAsync(ct))(e)) return null;
        var account = await accounts.GetAsync(e.UserId, ct);
        var (deptNames, siteNames, gradeNames) = await NamesAsync(ct);
        var dept = await departments.GetByIdAsync(e.DepartmentId, ct);
        var division = dept is null ? null : await divisions.GetByIdAsync(dept.DivisionId, ct);
        var history = (await assignments.ListAsync(a => a.EmployeeId == e.Id, ct)).OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id).ToList();
        var people = await users.GetDisplayNamesAsync(history.SelectMany(a => new[] { a.ReportsToUserId, a.RecordedByUserId })
            .Append(e.ReportsToUserId).Append(dept?.HeadOfDepartmentUserId).OfType<string>().Distinct(), ct);
        string? Name(string? uid) => uid is null ? null : people.GetValueOrDefault(uid, uid);

        var onboarding = e.OnboardingRecordId is { } oid ? await onboardings.GetByIdAsync(oid, ct) : null;
        var file = (await personnelFiles.ListAsync(f => f.EmployeeUserId == e.UserId, ct)).FirstOrDefault();
        var probation = (await probations.ListAsync(p => p.EmployeeUserId == e.UserId, ct)).OrderByDescending(p => p.Id).FirstOrDefault();
        var lineManager = e.ReportsToUserId is not null ? ""
            : dept?.HeadOfDepartmentUserId is { } hod && hod != e.UserId ? $"No line manager set: the Head of Department, {Name(hod)}, acts as line manager."
            : "No line manager set, and the department has no Head of Department.";

        return new EmployeeDetailsDto(e.Id, e.EmployeeNumber, account?.FullName ?? e.EmployeeNumber, account?.Email, account?.Phone, account?.IsActive ?? false,
            e.PositionTitle, e.DepartmentId, deptNames.GetValueOrDefault(e.DepartmentId, ""), division?.Name ?? "", e.SiteId, siteNames.GetValueOrDefault(e.SiteId, ""),
            e.GradeId, e.GradeId is { } g ? gradeNames.GetValueOrDefault(g) : null, e.ReportsToUserId, Name(e.ReportsToUserId), lineManager,
            e.EmploymentType, e.ContractEndDate, e.HireDate, e.ConfirmationDate, e.Status,
            onboarding?.Id, onboarding?.Number, file?.Id, file?.FileNumber, probation?.Id, probation?.Number, probation?.Status,
            history.Select(a => new EmployeeAssignmentDto(a.EffectiveFrom, a.EffectiveTo, a.PositionTitle, deptNames.GetValueOrDefault(a.DepartmentId, ""),
                siteNames.GetValueOrDefault(a.SiteId, ""), a.GradeId is { } ag ? gradeNames.GetValueOrDefault(ag) : null, Name(a.ReportsToUserId),
                a.EmploymentType, a.ContractEndDate, a.Source, a.Note, Name(a.RecordedByUserId))).ToList(),
            MayManage && e.UserId != me.UserId && e.Status != EmployeeStatus.Separated, e.Version);
    }

    /// <summary>The employee record of a user account, if any (for links from other pages).</summary>
    public async Task<int?> IdForUserAsync(string userId, CancellationToken ct = default) =>
        (await employees.ListAsync(e => e.UserId == userId, ct)).FirstOrDefault() is { } e && (await VisibleAsync(ct))(e) ? e.Id : null;

    private async Task<(Dictionary<int, string> Departments, Dictionary<int, string> Sites, Dictionary<int, string> Grades)> NamesAsync(CancellationToken ct) =>
        ((await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name),
         (await sites.ListAsync(ct: ct)).ToDictionary(s => s.Id, s => s.Name),
         (await grades.ListAsync(ct: ct)).ToDictionary(g => g.Id, GradeService.Label));

    // ===================================================================== Registering existing staff

    public async Task<Result<int>> RegisterAsync(RegisterEmployeeForm form, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure<int>("You may not register employees.");
        Normalize(form);
        var errors = await ValidateRegistrationAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);
        return await RegisterValidAsync(form, ct);
    }

    private async Task<Result<int>> RegisterValidAsync(RegisterEmployeeForm form, CancellationToken ct)
    {
        var account = await accounts.RegisterStaffAsync(form.FullName, form.Email, form.Phone, form.EmployeeNumber, form.DepartmentId!.Value, form.SiteId!.Value, ct);
        if (!account.Succeeded) return Result.Failure<int>([.. account.Errors]);

        var now = DateTime.UtcNow;
        var e = new Employee
        {
            EmployeeNumber = form.EmployeeNumber, UserId = account.Value!, PositionTitle = form.PositionTitle, DepartmentId = form.DepartmentId!.Value,
            SiteId = form.SiteId!.Value, GradeId = form.GradeId, ReportsToUserId = form.ReportsToUserId, EmploymentType = form.EmploymentType,
            ContractEndDate = form.ContractEndDate, HireDate = form.HireDate!.Value, Status = form.Status,
            ConfirmationDate = form.Status == EmployeeStatus.Active ? form.ConfirmationDate : null, StatusChangedAt = now
        };
        e.Assignments.Add(new EmployeeAssignment
        {
            EffectiveFrom = e.HireDate, PositionTitle = e.PositionTitle, DepartmentId = e.DepartmentId, SiteId = e.SiteId, GradeId = e.GradeId,
            ReportsToUserId = e.ReportsToUserId, EmploymentType = e.EmploymentType, ContractEndDate = e.ContractEndDate,
            Source = AssignmentSource.Registration, Note = "Registered by HR (hired before GRSMS).", RecordedByUserId = me.UserId
        });
        employees.Add(e);
        var saved = await unitOfWork.TrySaveAsync(ConflictMessage, ct);
        return saved.Succeeded ? Result.Success(e.Id) : Result.Failure<int>([.. saved.Errors]);
    }

    private static void Normalize(RegisterEmployeeForm form)
    {
        form.FullName = Collapse(form.FullName);
        form.Email = (form.Email ?? "").Trim();
        form.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        form.EmployeeNumber = (form.EmployeeNumber ?? "").Trim().ToUpperInvariant();
        form.PositionTitle = Collapse(form.PositionTitle);
        form.ReportsToUserId = string.IsNullOrWhiteSpace(form.ReportsToUserId) ? null : form.ReportsToUserId;
    }

    private static string Collapse(string? s) => string.Join(' ', (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task<List<string>> ValidateRegistrationAsync(RegisterEmployeeForm form, CancellationToken ct)
    {
        var errors = new List<string>();
        if (form.FullName.Length is < 2 or > 150) errors.Add("Enter the employee's full name.");
        if (!System.Net.Mail.MailAddress.TryCreate(form.Email, out _) || form.Email.Length > 256) errors.Add("Enter a valid email address.");
        if (form.EmployeeNumber.Length is 0 or > 30) errors.Add("Enter the staff number from payroll (30 characters at most).");
        else if (await employees.AnyAsync(x => x.EmployeeNumber == form.EmployeeNumber, ct))
            errors.Add($"Staff number {form.EmployeeNumber} is already registered.");
        if (form.Email.Length > 0 && await accounts.FindByEmailAsync(form.Email, ct) is { } existing)
        {
            if (existing.IsCandidate) errors.Add($"{form.Email} belongs to a candidate account. Candidates become employees through onboarding.");
            else if (await employees.AnyAsync(x => x.UserId == existing.Id, ct)) errors.Add($"{existing.FullName} ({form.Email}) is already registered as an employee.");
        }
        errors.AddRange(await ValidateJobAsync(form.PositionTitle, form.DepartmentId, form.SiteId, form.GradeId, form.ReportsToUserId, form.EmploymentType,
            form.ContractEndDate, form.HireDate, null, ct));
        if (form.HireDate is { } hired)
        {
            if (hired > Today) errors.Add("The date joined cannot be in the future. New employees join through onboarding.");
            if (hired < new DateOnly(1950, 1, 1)) errors.Add("Check the date joined.");
        }
        if (form.Status == EmployeeStatus.Separated) errors.Add("Register current staff only.");
        if (form.ConfirmationDate is { } c && (c > Today || (form.HireDate is { } h && c < h))) errors.Add("The confirmation date must be between the date joined and today.");
        return errors;
    }

    /// <summary>The job details shared by registration and updates.</summary>
    private async Task<List<string>> ValidateJobAsync(string position, int? departmentId, int? siteId, int? gradeId, string? reportsTo, EmploymentType type,
        DateOnly? contractEnd, DateOnly? from, Employee? self, CancellationToken ct)
    {
        var errors = new List<string>();
        if (position.Length is 0 or > 150) errors.Add("Enter the position (150 characters at most).");
        if (departmentId is not { } d || await departments.GetByIdAsync(d, ct) is not { IsActive: true }) errors.Add("Choose an active department.");
        if (siteId is not { } s || await sites.GetByIdAsync(s, ct) is not { IsActive: true }) errors.Add("Choose an active site.");
        if (gradeId is { } g && await grades.GetByIdAsync(g, ct) is not { IsActive: true } && self?.GradeId != g) errors.Add("Choose an active grade.");
        if (!Enum.IsDefined(type)) errors.Add("Choose the employment type.");
        if (reportsTo is not null)
        {
            if (self is not null && reportsTo == self.UserId) errors.Add("An employee cannot report to themselves.");
            else if (!await users.IsActiveStaffAsync(reportsTo, ct)) errors.Add("The line manager must be an active member of staff.");
        }
        if (contractEnd is { } end && from is { } start && end < start) errors.Add("The contract end date must be after the start.");
        return errors;
    }

    // ===================================================================== Updating job details

    public async Task<UpdateJobForm?> GetUpdateFormAsync(int id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct) is not { CanManage: true } d) return null;
        return new UpdateJobForm
        {
            EmployeeId = d.Id, Version = d.Version, EffectiveFrom = Today, PositionTitle = d.PositionTitle, DepartmentId = d.DepartmentId, SiteId = d.SiteId,
            GradeId = d.GradeId, ReportsToUserId = d.ReportsToUserId, EmploymentType = d.EmploymentType, ContractEndDate = d.ContractEndDate
        };
    }

    public async Task<Result> UpdateJobAsync(UpdateJobForm form, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure("You may not update employees.");
        if (await employees.GetByIdAsync(form.EmployeeId, ct) is not { } e || !(await VisibleAsync(ct))(e)) return Result.Failure("Employee not found.");
        if (e.UserId == me.UserId) return Result.Failure("You cannot change your own job details; another member of HR must do it.");
        if (e.Status == EmployeeStatus.Separated) return Result.Failure("This employee has left GREL; their record can no longer change.");
        if (form.Version != e.Version) return Result.Failure(ConflictMessage);

        form.PositionTitle = Collapse(form.PositionTitle);
        form.Note = (form.Note ?? "").Trim();
        form.ReportsToUserId = string.IsNullOrWhiteSpace(form.ReportsToUserId) ? null : form.ReportsToUserId;
        var open = (await assignments.ListAsync(a => a.EmployeeId == e.Id && a.EffectiveTo == null, ct)).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
        var errors = await ValidateJobAsync(form.PositionTitle, form.DepartmentId, form.SiteId, form.GradeId, form.ReportsToUserId, form.EmploymentType,
            form.ContractEndDate, form.EffectiveFrom, e, ct);
        if (form.Note.Length is < 3 or > 500) errors.Add("Say why the job details change (3 to 500 characters).");
        if (form.EffectiveFrom is not { } from) errors.Add("Enter the date the change takes effect.");
        else
        {
            if (from > Today) errors.Add("The change cannot take effect in the future. Enter it on the day it takes effect.");
            if (from < (open?.EffectiveFrom ?? e.HireDate)) errors.Add($"The change cannot take effect before the current job details ({(open?.EffectiveFrom ?? e.HireDate):d MMM yyyy}).");
        }
        if (errors.Count > 0) return Result.Failure([.. errors]);
        if ((e.PositionTitle, e.DepartmentId, e.SiteId, e.GradeId, e.ReportsToUserId, e.EmploymentType, e.ContractEndDate) ==
            (form.PositionTitle, form.DepartmentId!.Value, form.SiteId!.Value, form.GradeId, form.ReportsToUserId, form.EmploymentType, form.ContractEndDate))
            return Result.Failure("Nothing changed.");

        if (e.DepartmentId != form.DepartmentId || e.SiteId != form.SiteId)
        {
            var moved = await accounts.UpdatePlacementAsync(e.UserId, form.DepartmentId!.Value, form.SiteId!.Value, ct);
            if (!moved.Succeeded) return moved;
        }
        var effective = form.EffectiveFrom!.Value;
        if (open is not null) open.EffectiveTo = effective > open.EffectiveFrom ? effective.AddDays(-1) : open.EffectiveFrom;
        (e.PositionTitle, e.DepartmentId, e.SiteId, e.GradeId, e.ReportsToUserId, e.EmploymentType, e.ContractEndDate) =
            (form.PositionTitle, form.DepartmentId!.Value, form.SiteId!.Value, form.GradeId, form.ReportsToUserId, form.EmploymentType, form.ContractEndDate);
        e.Version = Guid.NewGuid();
        e.Assignments.Add(new EmployeeAssignment
        {
            EmployeeId = e.Id, EffectiveFrom = effective, PositionTitle = e.PositionTitle, DepartmentId = e.DepartmentId, SiteId = e.SiteId, GradeId = e.GradeId,
            ReportsToUserId = e.ReportsToUserId, EmploymentType = e.EmploymentType, ContractEndDate = e.ContractEndDate,
            Source = AssignmentSource.Update, Note = form.Note, RecordedByUserId = me.UserId
        });
        return await unitOfWork.TrySaveAsync(ConflictMessage, ct);
    }

    // ===================================================================== CSV import

    /// <summary>The columns of the import file, in the order of the template.</summary>
    public static readonly string[] ImportColumns =
        ["StaffNumber", "FullName", "Email", "Phone", "Position", "Department", "Site", "Grade", "EmploymentType", "HireDate", "Status", "ConfirmationDate", "ReportsToEmail", "ContractEndDate"];

    private static readonly string[] RequiredColumns = ["StaffNumber", "FullName", "Email", "Position", "Department", "Site", "HireDate"];

    /// <summary>A template with the header row and one example line.</summary>
    public static string ImportTemplate() =>
        string.Join(",", ImportColumns) + "\r\n" +
        "GREL-0456,Kwame Boateng,k.boateng@grel.com.gh,0244000000,HR Officer,Human Resources,Head Office,,Permanent,2019-03-01,Active,2019-09-01,,\r\n";

    /// <summary>
    /// Imports existing staff from a CSV file (UTF-8, comma-separated, header row first). Every row is checked first; if any
    /// row is wrong nothing is imported and the errors are listed by row. A line manager may be someone else in the file.
    /// </summary>
    public async Task<EmployeeImportResult> ImportAsync(Stream csv, CancellationToken ct = default)
    {
        if (!MayManage) return new(0, ["You may not register employees."]);
        List<string[]> rows;
        using (var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            rows = ParseCsv(await reader.ReadToEndAsync(ct));
        rows = rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        if (rows.Count < 2) return new(0, ["The file has no employees. Use the template: a header row, then one employee per line."]);
        if (rows.Count - 1 > MaxImportRows) return new(0, [$"Import at most {MaxImportRows} employees at a time."]);

        var header = rows[0].Select(h => h.Replace(" ", "").Trim()).ToList();
        var missing = RequiredColumns.Where(c => !header.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0) return new(0, [$"The header row is missing: {string.Join(", ", missing)}. Use the template."]);
        int Col(string name) => header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        var cols = ImportColumns.ToDictionary(c => c, Col);

        var deptList = await departments.ListAsync(d => d.IsActive, ct);
        var siteList = await sites.ListAsync(s => s.IsActive, ct);
        var gradeList = await grades.ListAsync(g => g.IsActive, ct);
        var fileEmails = rows.Skip(1).Select(r => Cell(r, cols["Email"])).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var pending = new List<(int Row, RegisterEmployeeForm Form, string? ManagerEmail)>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            var row = i + 1; // as numbered in a spreadsheet
            string C(string name) => Cell(r, cols[name]);
            var rowErrors = new List<string>();

            var dept = Match(deptList, C("Department"), d => d.Name, d => d.Code);
            var site = Match(siteList, C("Site"), s => s.Name, s => s.Code);
            var grade = C("Grade").Length == 0 ? null : Match(gradeList, C("Grade"), g => g.Name, g => g.Code);
            if (C("Department").Length > 0 && dept is null) rowErrors.Add($"no active department '{C("Department")}'");
            if (C("Site").Length > 0 && site is null) rowErrors.Add($"no active site '{C("Site")}'");
            if (C("Grade").Length > 0 && grade is null) rowErrors.Add($"no active grade '{C("Grade")}'");
            var type = EmploymentType.Permanent;
            if (C("EmploymentType").Length > 0 && !TryEnum(C("EmploymentType"), out type)) rowErrors.Add($"unknown employment type '{C("EmploymentType")}'");
            var status = EmployeeStatus.Active;
            if (C("Status").Length > 0 && !TryEnum(C("Status"), out status)) rowErrors.Add($"unknown status '{C("Status")}' (Active or On probation)");
            DateOnly? Date(string name)
            {
                if (C(name).Length == 0) return null;
                if (DateOnly.TryParseExact(C(name), ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d MMM yyyy"], CultureInfo.GetCultureInfo("en-GB"),
                        DateTimeStyles.None, out var d)) return d;
                rowErrors.Add($"{name} '{C(name)}' is not a date (use 2019-03-01 or 01/03/2019)");
                return null;
            }

            var managerEmail = C("ReportsToEmail");
            string? managerId = null;
            if (managerEmail.Length > 0)
            {
                var manager = await accounts.FindByEmailAsync(managerEmail, ct);
                if (manager is { IsCandidate: false, IsActive: true }) managerId = manager.Id;
                else if (!fileEmails.Contains(managerEmail)) rowErrors.Add($"the line manager {managerEmail} is not an active member of staff or in the file");
            }

            var form = new RegisterEmployeeForm
            {
                EmployeeNumber = C("StaffNumber"), FullName = C("FullName"), Email = C("Email"), Phone = C("Phone"), PositionTitle = C("Position"),
                DepartmentId = dept?.Id, SiteId = site?.Id, GradeId = grade?.Id, EmploymentType = type, HireDate = Date("HireDate"), Status = status,
                ConfirmationDate = Date("ConfirmationDate"), ContractEndDate = Date("ContractEndDate"), ReportsToUserId = managerId
            };
            Normalize(form);
            if (form.HireDate is null && C("HireDate").Length == 0) rowErrors.Add("HireDate is empty");
            if (form.EmployeeNumber.Length > 0 && !seenNumbers.Add(form.EmployeeNumber)) rowErrors.Add($"staff number {form.EmployeeNumber} appears twice in the file");
            if (form.Email.Length > 0 && !seenEmails.Add(form.Email)) rowErrors.Add($"{form.Email} appears twice in the file");
            if (rowErrors.Count == 0) rowErrors.AddRange((await ValidateRegistrationAsync(form, ct)).Select(Lower));
            if (rowErrors.Count > 0) errors.Add($"Row {row} ({(form.FullName.Length > 0 ? form.FullName : "no name")}): {string.Join("; ", rowErrors.Distinct())}.");
            else pending.Add((row, form, managerId is null && managerEmail.Length > 0 ? managerEmail : null));
        }
        if (errors.Count > 0) return new(0, errors);

        // Every row is valid: register them in order, then set the line managers who were in the file.
        var imported = new List<(int EmployeeId, string? ManagerEmail)>();
        foreach (var (row, form, managerEmail) in pending)
        {
            var result = await RegisterValidAsync(form, ct);
            if (!result.Succeeded)
                return new(imported.Count, [$"Row {row} ({form.FullName}): {string.Join(" ", result.Errors)} The rows before it were imported; fix this row and import the rest."]);
            imported.Add((result.Value, managerEmail));
        }
        foreach (var (employeeId, managerEmail) in imported.Where(x => x.ManagerEmail is not null))
        {
            if (await accounts.FindByEmailAsync(managerEmail!, ct) is not { } manager || await employees.GetByIdAsync(employeeId, ct) is not { } e || manager.Id == e.UserId) continue;
            e.ReportsToUserId = manager.Id;
            foreach (var a in await assignments.ListAsync(a => a.EmployeeId == employeeId && a.EffectiveTo == null, ct)) a.ReportsToUserId = manager.Id;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new(imported.Count, []);
    }

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..].TrimEnd('.');

    private static string Cell(string[] row, int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";

    private static T? Match<T>(IEnumerable<T> items, string text, Func<T, string> name, Func<T, string> code) where T : class =>
        text.Length == 0 ? null
            : items.FirstOrDefault(x => string.Equals(name(x), text, StringComparison.OrdinalIgnoreCase))
              ?? items.FirstOrDefault(x => string.Equals(code(x), text, StringComparison.OrdinalIgnoreCase));

    /// <summary>An enum by its name ("FixedTermContract") or its label ("Contract", "On probation").</summary>
    private static bool TryEnum<T>(string text, out T value) where T : struct, Enum
    {
        var t = text.Replace(" ", "");
        foreach (var v in Enum.GetValues<T>())
            if (string.Equals(v.ToString(), t, StringComparison.OrdinalIgnoreCase) || string.Equals(EnumText.Label(v).Replace(" ", ""), t, StringComparison.OrdinalIgnoreCase))
            {
                value = v;
                return true;
            }
        value = default;
        return false;
    }

    /// <summary>A small CSV reader: commas, double-quoted fields (with "" for a quote), CRLF or LF line ends.</summary>
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add([.. row]); row.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return rows;
    }
}
