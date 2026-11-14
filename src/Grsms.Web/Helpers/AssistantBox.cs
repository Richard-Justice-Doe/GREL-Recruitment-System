namespace Grsms.Web.Helpers;

/// <summary>
/// docs/36 AI-6: where the onboarding assistant (Views/Shared/_OnboardingAssistant.cshtml) is shown. The new employee's pages pass
/// their application; the staff onboarding page passes the onboarding. <see cref="ForEmployee"/> chooses the wording and the
/// suggested questions.
/// </summary>
/// <param name="Folded">Start folded to one line (e.g. on the documents page, where on a phone it comes before the checklist).</param>
public sealed record AssistantBox(int? ApplicationId, int? OnboardingId, bool Folded = false)
{
    public bool ForEmployee => ApplicationId is not null;

    /// <summary>Open at first: on the new employee's pages unless folded; folded on the staff page.</summary>
    public bool Open => ForEmployee && !Folded;
}
