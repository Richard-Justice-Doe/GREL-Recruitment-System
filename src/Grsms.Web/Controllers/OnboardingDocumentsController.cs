using Grsms.Application.Onboarding;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 27: the configurable onboarding document checklist (name, description, group, when it is required, file types, files
/// needed, personnel file category, order, active). For holders of "Maintain organisation data". A change applies at once to
/// the onboardings not yet activated.
/// </summary>
[Authorize(Policy = Policies.MasterData)]
public class OnboardingDocumentsController(OnboardingDocumentTypeService types) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await types.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Save(OnboardingDocumentTypeForm form, CancellationToken ct)
    {
        var result = await types.SaveAsync(form, ct);
        if (result.Succeeded)
        {
            Success(form.Id is null
                ? "Document added. It has been added to every onboarding not yet activated."
                : "Document saved. The change applies at once to every onboarding not yet activated.");
            return Redirect(Url.Action(nameof(Index)) + "#type-" + result.Value);
        }
        Fail(result);
        return Redirect(Url.Action(nameof(Index)) + (form.Id is int id ? "#type-" + id : "#add"));
    }
}
