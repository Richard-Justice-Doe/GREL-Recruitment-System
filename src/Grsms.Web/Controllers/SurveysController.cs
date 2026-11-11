using Grsms.Application.Surveys;
using Grsms.Domain.Surveys;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Feedback surveys (Module 10): the invited person answers (Take); HR reads the results without names (Results).
/// Rules live in <see cref="SurveyService"/>.
/// </summary>
[Authorize]
public class SurveysController(SurveyService surveys) : AppController
{
    /// <param name="id">The invitation id.</param>
    public async Task<IActionResult> Take(int id, CancellationToken ct)
    {
        var model = await surveys.GetFormAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>Ratings arrive as "rating_{key}" = 1-5 (empty = not applicable), comments as "text_{key}".</summary>
    [HttpPost]
    [ActionName("Take")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        var form = await surveys.GetFormAsync(id, ct);
        if (form is null) return NotFound();
        var ratings = new Dictionary<string, int?>();
        var texts = new Dictionary<string, string?>();
        foreach (var q in form.Questions)
        {
            if (q.IsRating) ratings[q.Key] = int.TryParse(Request.Form[$"rating_{q.Key}"], out var r) ? r : null;
            else texts[q.Key] = Request.Form[$"text_{q.Key}"];
        }

        var result = await surveys.SubmitAsync(id, ratings, texts, ct);
        if (result.Succeeded)
        {
            Success("Thank you for your feedback!");
            return RedirectToAction("Index", "Home");
        }
        Fail(result);
        return RedirectToAction(nameof(Take), new { id });
    }

    [Authorize(Policy = Policies.SurveyResults)]
    public async Task<IActionResult> Results([FromQuery] SurveyResultsFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        var model = await surveys.ResultsAsync(filter, ct);
        return model is null ? Forbid() : View(model);
    }
}
