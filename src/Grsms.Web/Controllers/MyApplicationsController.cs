using Grsms.Application.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The applicant's side of applications (Module 5): apply through a live advert, see my applications and their
/// progress, withdraw. Any signed-in person may use it (candidates, and GREL staff for internal jobs).
/// Rules live in <see cref="ApplicationService"/>.
/// </summary>
[Authorize]
public class MyApplicationsController(ApplicationService applications, Application.Interviews.InterviewService interviews,
    Application.Hiring.HiringService hiring, Application.Onboarding.OnboardingService onboarding) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await applications.MyApplicationsAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await applications.GetMineAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Interviews = await interviews.ForMyApplicationAsync(id, ct); // Phase 7: date, place and instructions only
        ViewBag.Offer = await hiring.GetMyOfferAsync(id, ct); // Phase 8
        ViewBag.Appointment = await onboarding.GetMyAppointmentAsync(id, ct); // Phase 9
        return View(model);
    }

    /// <summary>The appointment letter (printable), with the signature box while it is unsigned.</summary>
    public async Task<IActionResult> Appointment(int id, CancellationToken ct)
    {
        var model = await onboarding.GetMyAppointmentAsync(id, ct);
        if (model is null || !model.LetterReady) return NotFound();
        ViewBag.ApplicationId = id;
        return View(model);
    }

    /// <summary>Sign the appointment letter by typing my full name.</summary>
    [HttpPost]
    public async Task<IActionResult> SignLetter(int id, string? fullName, bool confirm, CancellationToken ct)
    {
        if (!confirm)
        {
            Error("Tick the box to confirm you accept the appointment on these terms.");
            return RedirectToAction(nameof(Appointment), new { id });
        }
        var result = await onboarding.SignLetterAsync(id, fullName, ct);
        if (result.Succeeded)
        {
            Success("Thank you! Your appointment letter is signed. See you on your first day.");
            return RedirectToAction(nameof(Details), new { id });
        }
        Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Appointment), new { id });
    }

    /// <param name="id">The advert number, e.g. ADV-2026-0001.</param>
    public async Task<IActionResult> Apply(string id, CancellationToken ct)
    {
        var result = await applications.GetApplyFormAsync(id, ct);
        if (result.Succeeded) return View(result.Value);

        Error(string.Join(" ", result.Errors));
        var status = await applications.GetApplyStatusAsync(id, ct);
        return status.ExistingApplicationId is int existing
            ? RedirectToAction(nameof(Details), new { id = existing })
            : RedirectToAction("Index", "Careers");
    }

    [HttpPost]
    public async Task<IActionResult> Apply(ApplyFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await applications.ApplyAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Application sent. We have emailed you a confirmation with your reference number.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await applications.FillApplyFormAsync(form, ct);
        return View(form);
    }

    /// <summary>The offer letter (printable).</summary>
    public async Task<IActionResult> Offer(int id, CancellationToken ct)
    {
        var offer = await hiring.GetMyOfferAsync(id, ct);
        if (offer is null) return NotFound();
        ViewBag.ApplicationId = id;
        return View(offer);
    }

    /// <summary>Accept or decline the offer.</summary>
    [HttpPost]
    public async Task<IActionResult> RespondOffer(int id, bool accept, string? reason, bool confirm, CancellationToken ct)
    {
        if (accept && !confirm)
        {
            Error("Tick the box to confirm you accept the terms of the offer.");
            return RedirectToAction(nameof(Offer), new { id });
        }
        var result = await hiring.RespondToOfferAsync(id, accept, reason, ct);
        if (result.Succeeded) Success(accept ? "Thank you! You have accepted the offer. HR will contact you about your first day." : "You have declined the offer.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        var result = await applications.WithdrawAsync(id, ct);
        if (result.Succeeded) Success("Your application has been withdrawn.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var file = await applications.OpenMyDocumentAsync(id, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
