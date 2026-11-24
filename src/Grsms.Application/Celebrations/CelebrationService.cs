using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Employees;
using Grsms.Application.Notifications;
using Grsms.Application.Onboarding;
using Grsms.Application.Settings;
using Grsms.Application.Users;
using Grsms.Domain.Celebrations;
using Grsms.Domain.Employees;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;

namespace Grsms.Application.Celebrations;

/// <summary>
/// One person celebrated on a date. <see cref="Date"/> is the celebration's date in the year asked for; <see cref="Years"/> the
/// years of service for an anniversary (null for a birthday: the year of birth and the age are never shown).
/// </summary>
public sealed record Celebrant(int EmployeeId, string UserId, string Name, string PositionTitle, int DepartmentId, string DepartmentName, int SiteId,
    string SiteName, bool HasPhoto, CelebrationKind Kind, DateOnly Date, int? Years)
{
    /// <summary>1, 5, 10, 15 … years: shown as a milestone.</summary>
    public bool Milestone => Years is { } y && (y == 1 || y % 5 == 0);
}

/// <summary>The profile's "Birthday: 15 November, in 45 days" and next work anniversary.</summary>
/// <param name="EnteredDateOfBirth">22 Nov 2026: the date of birth entered by HR or the employee (staff without a personal record).</param>
/// <param name="FromPersonalRecord">The birthday comes from the onboarding personal record, so it is not entered here.</param>
public sealed record ProfileCelebrations(DateOnly? NextBirthday, int? DaysToBirthday, DateOnly NextAnniversary, int AnniversaryYears, int DaysToAnniversary, bool OptedOut,
    DateOnly? EnteredDateOfBirth = null, bool FromPersonalRecord = false);

/// <summary>22 Nov 2026: the signed-in employee's own birthday, for "Add your birthday" on the celebrations page.</summary>
/// <param name="IsEmployee">The person has an employee record (otherwise there is nothing to add).</param>
public sealed record MyBirthday(bool IsEmployee, bool FromPersonalRecord, DateOnly? DateOfBirth);

public sealed record CelebrationMessageDto(int Id, string AuthorName, string Text, DateTime At, bool IsHidden, bool Mine);

/// <summary>What the celebrations page, the dashboard widget and the Action Center show.</summary>
public sealed record CelebrationsDto(IReadOnlyList<Celebrant> Today, IReadOnlyList<Celebrant> Upcoming, bool BirthdaysOn, bool AnniversariesOn,
    bool MessagesOn, bool DepartmentOnly)
{
    public int BirthdaysToday => Today.Count(c => c.Kind == CelebrationKind.Birthday);
    public int AnniversariesToday => Today.Count(c => c.Kind == CelebrationKind.Anniversary);
}

/// <summary>
/// 17 November 2026: birthdays and work anniversaries.
///   * <b>Birthdays</b> come from the date of birth in the employee's personal record (collected during onboarding); nothing is typed
///     again. Only the day and month are ever shown. 29 February birthdays are celebrated on 28 February in other years.
///   * <b>Work anniversaries</b> come from the date joined: 1, 2, 3 … years, with 1, 5, 10, 15 … highlighted.
///   * Current employees only, and never someone HR has opted out at their request (<see cref="Employee.CelebrationsOptOut"/>).
///   * HR's settings (Celebrations page): birthdays and anniversaries on or off, in-app notifications, emails, company-wide or
///     department only, and colleagues' messages. With "department only", staff see and are told about their own department's
///     celebrations; HR (all departments) see everyone's.
///   * <see cref="RunMorningAsync"/> (the scheduled job, once a day from the configured hour): one notification (and, if switched
///     on, one email) to each member of staff listing the day's celebrations, and a greeting to each person celebrated.
/// </summary>
public class CelebrationService(
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IRepository<Site> sites,
    IRepository<CelebrationMessage> messages,
    IPersonalRecordRepository personalRecords,
    EmployeePhotoService photos,
    IUserDirectory users,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    ISettingsStore settings,
    ICurrentUserService me)
{
    public const int MaxMessage = 280, UpcomingDays = 7, MessageDaysAfter = 3;
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public bool BirthdaysOn => settings.GetBool(SettingCatalogue.CelebrateBirthdays);
    public bool AnniversariesOn => settings.GetBool(SettingCatalogue.CelebrateAnniversaries);
    public bool MessagesOn => settings.GetBool(SettingCatalogue.CelebrationMessages);
    public bool DepartmentOnly => settings.GetBool(SettingCatalogue.CelebrationDepartmentOnly);
    public bool IsStaff => me.UserId is not null && !me.IsInRole(Roles.Candidate);

    /// <summary>The celebration's date in <paramref name="year"/> (29 February → 28 February outside leap years).</summary>
    public static DateOnly InYear(DateOnly date, int year) =>
        date.Month == 2 && date.Day == 29 && !DateTime.IsLeapYear(year) ? new DateOnly(year, 2, 28) : new DateOnly(year, date.Month, date.Day);

    /// <summary>The next birthday on or after <paramref name="today"/>.</summary>
    public static DateOnly NextOccurrence(DateOnly date, DateOnly today)
    {
        var d = InYear(date, today.Year);
        return d >= today ? d : InYear(date, today.Year + 1);
    }

    // ===================================================================== Who is celebrated

    /// <summary>Everyone who can be celebrated, with their birth and joining dates (one load; GREL's staff fit easily in memory).</summary>
    private async Task<List<(Employee E, string Name, DateOnly? Birth, bool HasPhoto)>> PeopleAsync(CancellationToken ct)
    {
        var list = await employees.ListAsync(e => e.Status != EmployeeStatus.Separated && !e.CelebrationsOptOut, ct);
        var names = await users.GetDisplayNamesAsync(list.Select(e => e.UserId).Distinct(), ct);
        var births = await personalRecords.BirthDatesAsync(list.Select(e => e.OnboardingRecordId).OfType<int>().ToList(), ct);
        var withPhoto = await photos.WithPhotoAsync(list.Select(e => (e.Id, e.PhotoAttachmentId, e.OnboardingRecordId)).ToList(), ct);
        // The personal record's date of birth wins; otherwise the one HR or the employee entered (22 Nov 2026).
        return list.Select(e => (e, names.GetValueOrDefault(e.UserId, e.EmployeeNumber),
            e.OnboardingRecordId is { } ob && births.TryGetValue(ob, out var b) ? b : e.DateOfBirth, withPhoto.Contains(e.Id))).ToList();
    }

    /// <summary>Every celebration between two dates (inclusive), oldest first.</summary>
    public async Task<IReadOnlyList<Celebrant>> BetweenAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var people = await PeopleAsync(ct);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var siteNames = (await sites.ListAsync(ct: ct)).ToDictionary(s => s.Id, s => s.Name);
        var result = new List<Celebrant>();
        foreach (var (e, name, birth, hasPhoto) in people)
        {
            Celebrant Make(CelebrationKind kind, DateOnly date, int? years) => new(e.Id, e.UserId, name, e.PositionTitle, e.DepartmentId,
                deptNames.GetValueOrDefault(e.DepartmentId, ""), e.SiteId, siteNames.GetValueOrDefault(e.SiteId, ""), hasPhoto, kind, date, years);
            for (var year = from.Year; year <= to.Year; year++)
            {
                if (BirthdaysOn && birth is { } b && InYear(b, year) is var bd && bd >= from && bd <= to)
                    result.Add(Make(CelebrationKind.Birthday, bd, null));
                if (AnniversariesOn && InYear(e.HireDate, year) is var ad && ad >= from && ad <= to && year - e.HireDate.Year >= 1)
                    result.Add(Make(CelebrationKind.Anniversary, ad, year - e.HireDate.Year));
            }
        }
        return result.OrderBy(c => c.Date).ThenBy(c => c.Kind).ThenBy(c => c.Name).ToList();
    }

    /// <summary>What the signed-in member of staff may see: everyone, or with "department only" their own department (HR: everyone).</summary>
    private async Task<Func<Celebrant, bool>> VisibleAsync(CancellationToken ct)
    {
        if (!IsStaff) return _ => false;
        if (!DepartmentOnly || me.HasPermission(Permissions.SeeAllDepartments)) return _ => true;
        var dept = me.UserId is null ? null : await users.GetDepartmentIdAsync(me.UserId, ct);
        return c => c.DepartmentId == dept;
    }

    /// <summary>Today's celebrations and the next <see cref="UpcomingDays"/> days', for the signed-in member of staff.</summary>
    public async Task<CelebrationsDto> OverviewAsync(DateOnly today, CancellationToken ct = default)
    {
        var visible = await VisibleAsync(ct);
        var all = (BirthdaysOn || AnniversariesOn) && IsStaff ? await BetweenAsync(today, today.AddDays(UpcomingDays), ct) : [];
        var mine = all.Where(visible).ToList();
        return new CelebrationsDto(mine.Where(c => c.Date == today).ToList(), mine.Where(c => c.Date > today).ToList(),
            BirthdaysOn, AnniversariesOn, MessagesOn, DepartmentOnly);
    }

    /// <summary>The calendar: one month, optionally for one department or site.</summary>
    public async Task<IReadOnlyList<Celebrant>> MonthAsync(int year, int month, int? departmentId, int? siteId, CancellationToken ct = default)
    {
        if (!IsStaff) return [];
        var first = new DateOnly(year, month, 1);
        var visible = await VisibleAsync(ct);
        return (await BetweenAsync(first, first.AddMonths(1).AddDays(-1), ct))
            .Where(visible).Where(c => (departmentId is null || c.DepartmentId == departmentId) && (siteId is null || c.SiteId == siteId)).ToList();
    }

    /// <summary>For the employee profile: the next birthday and work anniversary, and whether the employee is opted out.</summary>
    public async Task<ProfileCelebrations?> ForProfileAsync(int employeeId, DateOnly today, CancellationToken ct = default)
    {
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null) return null;
        var birthday = await BirthdayOfAsync(e, today, ct);
        var anniversary = NextOccurrence(e.HireDate, today);
        if (anniversary.Year == e.HireDate.Year) anniversary = InYear(e.HireDate, today.Year + 1); // the first one is a year after joining
        return new ProfileCelebrations(birthday?.Birthday, birthday?.DaysUntil, anniversary, anniversary.Year - e.HireDate.Year,
            anniversary.DayNumber - today.DayNumber, e.CelebrationsOptOut, e.DateOfBirth, await RecordBirthAsync(e, ct) is not null);
    }

    /// <summary>For the employee profile: the birthday (day and month) and how far away it is; null without a date of birth.</summary>
    public async Task<(DateOnly Birthday, int DaysUntil)?> BirthdayOfAsync(Employee e, DateOnly today, CancellationToken ct = default)
    {
        if ((await RecordBirthAsync(e, ct) ?? e.DateOfBirth) is not { } birth) return null;
        var next = NextOccurrence(birth, today);
        return (next, next.DayNumber - today.DayNumber);
    }

    /// <summary>The date of birth in the employee's onboarding personal record, if any.</summary>
    private async Task<DateOnly?> RecordBirthAsync(Employee e, CancellationToken ct) =>
        e.OnboardingRecordId is { } ob && (await personalRecords.BirthDatesAsync([ob], ct)).TryGetValue(ob, out var birth) ? birth : null;

    // ===================================================================== Entering a date of birth (22 Nov 2026)

    /// <summary>The signed-in member of staff's own birthday, for "Add your birthday".</summary>
    public async Task<MyBirthday> MineAsync(CancellationToken ct = default)
    {
        if (!IsStaff) return new(false, false, null);
        var e = (await employees.ListAsync(x => x.UserId == me.UserId && x.Status != EmployeeStatus.Separated, ct)).FirstOrDefault();
        if (e is null) return new(false, false, null);
        var record = await RecordBirthAsync(e, ct);
        return new(true, record is not null, record ?? e.DateOfBirth);
    }

    /// <summary>The employee enters (or clears) their own date of birth.</summary>
    public async Task<Result> SetMineAsync(DateOnly? dateOfBirth, CancellationToken ct = default)
    {
        if (!IsStaff) return Result.Failure("Only GREL staff have a birthday in celebrations.");
        var found = (await employees.ListAsync(x => x.UserId == me.UserId && x.Status != EmployeeStatus.Separated, ct)).FirstOrDefault();
        if (found is null || await employees.GetByIdAsync(found.Id, ct) is not { } e) return Result.Failure("You have no employee record yet. Ask HR to register you.");
        return await SetAsync(e, dateOfBirth, ct);
    }

    /// <summary>HR enters (or clears) an employee's date of birth.</summary>
    public async Task<Result> SetForAsync(int employeeId, DateOnly? dateOfBirth, CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.ManageEmployees)) return Result.Failure("Only HR can change this.");
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null) return Result.Failure("Employee not found.");
        return await SetAsync(e, dateOfBirth, ct);
    }

    private async Task<Result> SetAsync(Employee e, DateOnly? dateOfBirth, CancellationToken ct)
    {
        if (await RecordBirthAsync(e, ct) is not null)
            return Result.Failure("The date of birth comes from the personal record completed during onboarding, so it cannot be changed here.");
        if (dateOfBirth is { } dob && CheckDateOfBirth(dob, DateOnly.FromDateTime(DateTime.UtcNow)) is { } problem) return Result.Failure(problem);
        e.DateOfBirth = dateOfBirth;
        e.Version = Guid.NewGuid();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>A sensible date of birth for a member of staff: in the past, 15 to 100 years ago. Null when it is fine.</summary>
    public static string? CheckDateOfBirth(DateOnly dob, DateOnly today) =>
        dob > today.AddYears(-15) ? "The date of birth must be at least 15 years ago."
        : dob < today.AddYears(-100) ? "The date of birth must be less than 100 years ago."
        : null;

    // ===================================================================== Messages

    public async Task<IReadOnlyList<CelebrationMessageDto>> MessagesAsync(int employeeId, CelebrationKind kind, int year, CancellationToken ct = default)
    {
        if (!IsStaff) return [];
        var hr = me.HasPermission(Permissions.ManageEmployees);
        return (await messages.ListAsync(m => m.EmployeeId == employeeId && m.Kind == kind && m.Year == year && (hr || !m.IsHidden), ct))
            .OrderBy(m => m.CreatedAt).Select(m => new CelebrationMessageDto(m.Id, m.AuthorName, m.Text, m.CreatedAt, m.IsHidden, m.AuthorUserId == me.UserId)).ToList();
    }

    /// <summary>A colleague leaves a message on the day of the celebration or up to <see cref="MessageDaysAfter"/> days after.</summary>
    public async Task<Result> PostAsync(int employeeId, CelebrationKind kind, string? text, DateOnly today, CancellationToken ct = default)
    {
        if (!IsStaff || me.UserId is null) return Result.Failure("Only GREL staff can leave messages.");
        if (!MessagesOn) return Result.Failure("Messages are switched off.");
        var t = text?.Trim() ?? "";
        if (t.Length == 0) return Result.Failure("Write a message first.");
        if (t.Length > MaxMessage) return Result.Failure($"Keep the message under {MaxMessage} characters.");
        var c = (await BetweenAsync(today.AddDays(-MessageDaysAfter), today, ct)).FirstOrDefault(x => x.EmployeeId == employeeId && x.Kind == kind);
        if (c is null || !(await VisibleAsync(ct))(c)) return Result.Failure("Messages can be left on the day and for a few days after.");
        if (c.UserId == me.UserId) return Result.Failure("This is your own celebration.");
        messages.Add(new CelebrationMessage
        {
            EmployeeId = employeeId, Kind = kind, Year = c.Date.Year, AuthorUserId = me.UserId, AuthorName = me.FullName ?? me.UserName ?? "A colleague", Text = t
        });
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>HR hides (or shows again) a message.</summary>
    public async Task<Result> SetHiddenAsync(int messageId, bool hidden, CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.ManageEmployees)) return Result.Failure("Only HR can moderate messages.");
        var m = await messages.GetByIdAsync(messageId, ct);
        if (m is null) return Result.Failure("Message not found.");
        m.IsHidden = hidden;
        m.HiddenBy = hidden ? me.UserName : null;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>HR records an employee's wish not to be celebrated (or undoes it).</summary>
    public async Task<Result> SetOptOutAsync(int employeeId, bool optOut, CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.ManageEmployees)) return Result.Failure("Only HR can change this.");
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null) return Result.Failure("Employee not found.");
        e.CelebrationsOptOut = optOut;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ===================================================================== The morning announcement (scheduled job)

    /// <summary>
    /// Once a day (claimed per date, so restarts and hourly runs never repeat it): tells every member of staff (or, with "department
    /// only", the department) about the day's celebrations in one notification, by email too if switched on, and greets each person
    /// celebrated. Returns how many people were told.
    /// </summary>
    public async Task<int> RunMorningAsync(DateOnly today, CancellationToken ct = default)
    {
        var notify = settings.GetBool(SettingCatalogue.CelebrationNotifications);
        var email = settings.GetBool(SettingCatalogue.CelebrationEmails);
        if (!notify && !email || !BirthdaysOn && !AnniversariesOn) return 0;
        if (DateTime.UtcNow.Hour < settings.GetInt(SettingCatalogue.CelebrationFromHourGmt)) return 0;
        var todays = await BetweenAsync(today, today, ct);
        if (todays.Count == 0 || !await notifications.ClaimReminderAsync($"celebrations:{today:yyyy-MM-dd}", ct)) return 0;

        var link = links.Celebrations();
        var staff = await users.GetActiveStaffAsync(null, ct);
        var byDept = DepartmentOnly ? await DepartmentsOfAsync(staff.Select(s => s.Value), ct) : null;
        var told = 0;
        foreach (var person in staff)
        {
            var theirs = todays.Where(c => c.UserId != person.Value && (byDept is null || byDept.GetValueOrDefault(person.Value) == c.DepartmentId)).ToList();
            if (theirs.Count == 0) continue;
            var (subject, html) = Announcement(theirs);
            if (email) await mailer.ToUserIncludingMeAsync(person.Value, subject, html, link, "See today's celebrations"); // also adds the notification
            else await notifications.AddAsync(person.Value, subject, WorkflowMailer.PlainText(html), link, ct);
            told++;
        }
        foreach (var c in todays)
        {
            var greeting = c.Kind == CelebrationKind.Birthday
                ? ("🎂 Happy birthday from all of us at GREL!", "Wishing you a wonderful day and a successful year filled with happiness, good health and success.")
                : ($"🎉 Happy {Ordinal(c.Years!.Value)} work anniversary!", $"Thank you for {c.Years} year{(c.Years == 1 ? "" : "s")} at GREL. Congratulations from all of us!");
            await notifications.AddAsync(c.UserId, greeting.Item1, greeting.Item2, link, ct);
        }
        return told;
    }

    private async Task<Dictionary<string, int?>> DepartmentsOfAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var map = new Dictionary<string, int?>();
        foreach (var id in userIds) map[id] = await users.GetDepartmentIdAsync(id, ct);
        return map;
    }

    /// <summary>The announcement's subject and body (HTML) for a list of celebrations.</summary>
    public static (string Subject, string Html) Announcement(IReadOnlyList<Celebrant> todays)
    {
        var birthdays = todays.Where(c => c.Kind == CelebrationKind.Birthday).ToList();
        var anniversaries = todays.Where(c => c.Kind == CelebrationKind.Anniversary).ToList();
        string Who(Celebrant c) => $"<b>{WorkflowMailer.Enc(c.Name)}</b> ({WorkflowMailer.Enc(c.DepartmentName)})";
        var subject = birthdays.Count == 1 && anniversaries.Count == 0 ? $"🎉 Happy Birthday {birthdays[0].Name}!"
            : birthdays.Count > 0 ? $"🎂 Birthday celebration: {string.Join(", ", birthdays.Take(3).Select(b => b.Name))}{(birthdays.Count > 3 ? " and others" : "")}"
            : $"🎉 Work anniversaries today: {string.Join(", ", anniversaries.Take(3).Select(a => a.Name))}{(anniversaries.Count > 3 ? " and others" : "")}";
        var html = "";
        if (birthdays.Count > 0)
            html += $"<p>🎂 Today we celebrate the birthday of {Join(birthdays.Select(Who))}. We wish {(birthdays.Count == 1 ? "them" : "them all")} a successful year " +
                    "filled with happiness, good health and success. Join us in saying: <b>Happy Birthday!</b></p>";
        if (anniversaries.Count > 0)
            html += $"<p>🎉 Work anniversaries: {Join(anniversaries.Select(a => $"{Who(a)}, {a.Years} year{(a.Years == 1 ? "" : "s")} of service"))}. Congratulations and thank you!</p>";
        return (subject, html);
    }

    private static string Join(IEnumerable<string> parts)
    {
        var list = parts.ToList();
        return list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list.Take(list.Count - 1))} and {list[^1]}";
    }

    public static string Ordinal(int n) => n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
}
