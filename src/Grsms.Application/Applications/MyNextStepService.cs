using Grsms.Application.Common.Interfaces;
using Grsms.Application.Onboarding;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Onboarding;

namespace Grsms.Application.Applications;

/// <summary>Where a next step leads in the candidate's portal.</summary>
public enum MyStepTarget { Offer, Appointment, PersonalRecord, Documents, Application, Profile }

/// <summary>One thing the candidate (or new employee) should do now.</summary>
/// <param name="ApplicationId">The application it belongs to (0 for the profile).</param>
/// <param name="Urgent">True when there is a deadline (offer reply-by, documents due, interview soon).</param>
public record MyNextStepDto(MyStepTarget Target, int ApplicationId, string Title, string Detail, string Button, bool Urgent);

/// <summary>The candidate's onboarding in one line, for the "My Onboarding" menu link and dashboard.</summary>
public record MyOnboardingLinkDto(int ApplicationId, string JobTitle, int Percent);

/// <summary>
/// Usability U2 (10 November 2026): what the signed-in candidate or new employee should do next, most urgent first: answer an
/// offer, then the onboarding steps (sign the letter, the personal record, the documents, a correction), then an interview in the
/// next 7 days, then completing their profile. Read-only; every action still goes through its own page and service.
/// </summary>
public class MyNextStepService(
    IApplicationRepository applications,
    IRepository<Offer> offers,
    IRepository<OnboardingRecord> onboardings,
    IRepository<Interview> interviews,
    OnboardingProgressService progress,
    CandidateProfileService profiles,
    ICurrentUserService me)
{
    public async Task<IReadOnlyList<MyNextStepDto>> GetAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return [];
        var steps = new List<MyNextStepDto>();
        var mine = await applications.ListAsync(a => a.ApplicantUserId == uid, ct);
        var ids = mine.Select(a => a.Id).ToList();

        // 1. Offers waiting for an answer.
        foreach (var o in (await offers.ListAsync(o => ids.Contains(o.JobApplicationId) && o.Status == OfferStatus.Sent, ct)).OrderBy(o => o.ExpiresOn))
            steps.Add(new(MyStepTarget.Offer, o.JobApplicationId, $"Answer your offer: {o.JobTitle}",
                $"Please accept or decline by {o.ExpiresOn:dddd d MMMM}.", "See my offer", true));

        // 2. Onboarding steps, in the order they are best done.
        foreach (var o in await onboardings.ListAsync(o => ids.Contains(o.JobApplicationId), ct))
        {
            if (!OnboardingWorkflow.BeforeActivation(o.Status)) continue;
            var snap = await progress.SnapshotAsync(o, ct);
            var due = o.DocumentsDueDate is { } d ? $" Please finish by {d:dddd d MMMM}." : "";
            if (o.CorrectionMessage is not null)
                steps.Add(new(MyStepTarget.Documents, o.JobApplicationId, "HR asked for a correction", o.CorrectionMessage, "See what to correct", true));
            if (o.LetterIssued && !o.LetterAcknowledged)
                steps.Add(new(MyStepTarget.Appointment, o.JobApplicationId, $"Sign your appointment letter: {o.JobTitle}",
                    "Read it and sign it by typing your full name.", "Read and sign", true));
            if (!snap.PersonalRecordSigned)
                steps.Add(new(MyStepTarget.PersonalRecord, o.JobApplicationId, "Fill in your personal record",
                    "Your details, family, next of kin and beneficiaries, then sign.", "Fill it in", false));
            var docTodo = snap.Readiness.ForEmployee.Count(x => x.StartsWith("Upload") || x.StartsWith("Replace"));
            if (docTodo > 0)
                steps.Add(new(MyStepTarget.Documents, o.JobApplicationId, $"Upload your documents ({docTodo} still needed)",
                    $"You can take photos with your phone.{due}", "Upload documents", o.DocumentsDueDate is not null));
        }

        // 3. Interviews in the next 7 days (information: nothing to click but the details).
        var now = DateTime.UtcNow;
        var soon = now.AddDays(7);
        foreach (var i in (await interviews.ListAsync(i => ids.Contains(i.JobApplicationId) && i.Status == InterviewStatus.Scheduled &&
                                                           i.StartsAt >= now && i.StartsAt <= soon, ct)).OrderBy(i => i.StartsAt))
            steps.Add(new(MyStepTarget.Application, i.JobApplicationId, $"Your {Common.EnumText.Label(i.Type).ToLowerInvariant()} on {i.StartsAt:dddd d MMMM 'at' HH:mm}",
                i.Location, "See the details", (i.StartsAt - now).TotalDays <= 2));

        // 4. The profile, when something is missing (it makes applying quicker).
        var profile = await profiles.GetMineAsync(ct);
        if (profile.Missing.Count > 0)
            steps.Add(new(MyStepTarget.Profile, 0, "Complete your profile", $"Still missing: {string.Join(", ", profile.Missing).ToLowerInvariant()}.",
                "Complete my profile", false));
        return steps;
    }

    /// <summary>The candidate's onboarding in progress, if any (for the "My Onboarding" link).</summary>
    public async Task<MyOnboardingLinkDto?> MyOnboardingAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return null;
        var o = (await onboardings.ListAsync(x => x.EmployeeUserId == uid && x.Status != OnboardingStatus.Cancelled && x.Status != OnboardingStatus.Completed, ct))
            .OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (o is null) return null;
        return new MyOnboardingLinkDto(o.JobApplicationId, o.JobTitle, (await progress.SnapshotAsync(o, ct)).Percent);
    }
}
