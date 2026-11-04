using System.Globalization;
using System.Text;
using Grsms.Application.Reports;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Reports and the executive overview (Module 11), with a CSV download of every report. What each person sees is
/// decided by <see cref="ReportService"/> (Heads of Department: their own departments only).
/// </summary>
[Authorize(Policy = Policies.Reports)]
public class ReportsController(ReportService reports) : AppController
{
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        if (ReportKeys.All.All(r => r.Key != filter.Report)) filter.Report = ReportKeys.Overview;
        ViewBag.Filter = filter;
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await reports.RunAsync(filter, ct));
    }

    /// <summary>The report's table as a CSV file (opens in Excel), with the same filters.</summary>
    public async Task<IActionResult> Export([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        if (ReportKeys.All.All(r => r.Key != filter.Report)) return NotFound();
        var result = await reports.RunAsync(filter, ct);
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", result.Table.Columns.Select(c => Cell(c.Header))));
        foreach (var row in result.Table.Rows) csv.AppendLine(string.Join(",", row.Cells.Select(Cell)));
        if (result.Table.Totals is { } totals) csv.AppendLine(string.Join(",", totals.Select(Cell)));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(); // BOM: Excel reads the accents
        return File(bytes, "text/csv", $"grsms-{result.Key}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// One CSV value. Text typed by users (names, titles) is protected against spreadsheet formulas: a value starting with
    /// = + - @ is prefixed with an apostrophe, so Excel shows it as text instead of running it.
    /// </summary>
    public static string Cell(object? value)
    {
        var text = value switch
        {
            null => "",
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime t => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
