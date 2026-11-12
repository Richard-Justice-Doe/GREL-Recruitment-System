using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Notifications;
using Grsms.Application.Users;
using Grsms.Domain.Employees;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;

namespace Grsms.Application.Employees;

/// <summary>
/// 12 November 2026 (docs/34 quick win): contract expiry alerts. When an employee's contract ends in 90, 60, 30 or 7 days,
/// the HR Managers, the Head of Department, the line manager and the employee are emailed (and see it in the bell), once per
/// stage. Run by the daily job with the other reminders. If the job missed a day, the stage the contract is now in is sent.
/// </summary>
public class ContractAlertService(
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IUserDirectory users,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links)
{
    /// <summary>Days before the contract end date at which an alert goes out (largest first).</summary>
    public static readonly int[] Stages = [90, 60, 30, 7];
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Sends the alerts that are due. Returns how many contracts were alerted.</summary>
    public async Task<int> RunAsync(DateOnly today, CancellationToken ct = default)
    {
        var horizon = today.AddDays(Stages[0]);
        var ending = await employees.ListAsync(e => e.Status != EmployeeStatus.Separated && e.ContractEndDate != null
                                                    && e.ContractEndDate >= today && e.ContractEndDate <= horizon, ct);
        if (ending.Count == 0) return 0;
        var deptIds = ending.Select(e => e.DepartmentId).Distinct().ToList();
        var heads = (await departments.ListAsync(d => deptIds.Contains(d.Id), ct)).ToDictionary(d => d.Id, d => d.HeadOfDepartmentUserId);
        var names = await users.GetDisplayNamesAsync(ending.Select(e => e.UserId).Distinct(), ct);
        var sent = 0;

        foreach (var e in ending)
        {
            var end = e.ContractEndDate!.Value;
            var daysLeft = end.DayNumber - today.DayNumber;
            // The smallest stage the contract has reached (e.g. 45 days left → the 60-day alert).
            var stage = Stages.Where(s => daysLeft <= s).DefaultIfEmpty(0).Min();
            if (stage == 0 || !await notifications.ClaimReminderAsync($"contract-end:{e.Id}:{end:yyyyMMdd}:{stage}", ct)) continue;

            var name = names.GetValueOrDefault(e.UserId, e.EmployeeNumber);
            var when = end.ToString("dddd d MMMM yyyy", Gb);
            var subject = $"Contract ending in {daysLeft} day{(daysLeft == 1 ? "" : "s")}: {name}";
            var html = $"The contract of <b>{WorkflowMailer.Enc(name)}</b> ({WorkflowMailer.Enc(e.EmployeeNumber)}, {WorkflowMailer.Enc(e.PositionTitle)}) " +
                       $"ends on <b>{when}</b>. Please decide whether to renew it and update the job details in GRSMS.";
            var managers = new[] { heads.GetValueOrDefault(e.DepartmentId), e.ReportsToUserId }.OfType<string>().Where(u => u != e.UserId).Distinct().ToList();

            await mailer.ToRoleAsync(Roles.HrManager, managers.Append(e.UserId), subject, html, links.Employee(e.Id), "Open the employee record", ct);
            foreach (var manager in managers)
                await mailer.ToUserAsync(manager, subject, html, links.Employee(e.Id), "Open the employee record", ct);
            await mailer.ToUserAsync(e.UserId, $"Your contract ends on {end.ToString("d MMMM yyyy", Gb)}",
                $"Your contract as <b>{WorkflowMailer.Enc(e.PositionTitle)}</b> ends on <b>{when}</b>. HR and your Head of Department have been " +
                "told; please speak to them about what happens next.", links.Home(), "Open GRSMS", ct);
            sent++;
        }
        return sent;
    }
}
