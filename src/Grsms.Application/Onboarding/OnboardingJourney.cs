using Grsms.Application.Common.Interfaces;
using Grsms.Application.Process;
using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

/// <summary>One task under a journey stage (IT set-up, department induction), live from the onboarding checklist.</summary>
public sealed record JourneyTask(string Title, bool Done, bool NotNeeded, string? AssignedTo);

/// <summary>
/// One stage of the onboarding journey. <see cref="State"/>: Done (green tick), Current (in progress, blue; several can be in
/// progress at once before activation), Upcoming (grey). <see cref="Owner"/> is who acts at this stage.
/// </summary>
public sealed record JourneyStage(int Number, string Title, StageState State, string Status, string Owner, string? Detail, IReadOnlyList<JourneyTask> Tasks,
    bool YourTurn = false);

/// <summary>The whole journey, with the completion and the next thing to do.</summary>
public sealed record OnboardingJourneyDto(IReadOnlyList<JourneyStage> Stages, bool ForEmployee, bool Completed)
{
    public int Percent => (int)Math.Round(100.0 * Stages.Count(s => s.State == StageState.Done) / Math.Max(1, Stages.Count));

    /// <summary>The stage to show first: the new employee's own pending step, else the first one not done.</summary>
    public JourneyStage? Next => Stages.FirstOrDefault(s => s.State != StageState.Done && s.YourTurn) ?? Stages.FirstOrDefault(s => s.State != StageState.Done);
}

/// <summary>
/// G3 (15 November 2026, guided workflow): the onboarding journey, "Welcome to your onboarding journey" on the new employee's pages and
/// the same stages (in staff wording) on the onboarding page:
///   1 Offer accepted · 2 Appointment letter · 3 Personal information · 4 Document upload (x of y, the medical report) ·
///   5 Recruitment Officer verification · 6 Staff account activation · 7 IT set-up (live tasks) · 8 Department induction (live tasks) ·
///   9 Onboarding complete.
/// Worked out from what GRSMS already holds (status, letter, personal record, document checklist, onboarding tasks): no data of its
/// own. There is no separate HR approval stage: decision of 10 November 2026 (Phase 27), the RO verifies and activation follows.
/// </summary>
public static class OnboardingJourney
{
    private static readonly OnboardingStatus[] AfterVerification = [OnboardingStatus.AwaitingActivation, OnboardingStatus.AwaitingDepartmentalInduction, OnboardingStatus.Completed];
    private static readonly OnboardingStatus[] Activated = [OnboardingStatus.AwaitingDepartmentalInduction, OnboardingStatus.Completed];

    public static OnboardingJourneyDto Build(OnboardingStatus status, bool letterIssued, bool letterSigned, bool personalRecordSigned,
        DocumentChecklistDto? checklist, IEnumerable<(OnboardingTeam Team, string Title, OnboardingTaskStatus Status, string? AssignedTo)> tasks,
        bool forEmployee)
    {
        string Who(string you, string them) => forEmployee ? you : them;
        var employee = Who("You", "The new employee");
        var stages = new List<JourneyStage>();
        var taskList = tasks.ToList();
        var activated = Activated.Contains(status);

        stages.Add(new(1, "Offer accepted", StageState.Done, "Completed", employee, null, []));

        stages.Add(letterSigned
            ? new(2, "Appointment letter", StageState.Done, "Signed", employee, null, [])
            : letterIssued
                ? new(2, "Appointment letter", StageState.Current, Who("Ready: read and sign it", "Waiting for the new employee's signature"), employee, null, [], forEmployee)
                : new(2, "Appointment letter", StageState.Current, "Being prepared", "Recruitment Officer", Who("You will be emailed when it is ready.", null), []));

        stages.Add(personalRecordSigned
            ? new(3, "Personal information", StageState.Done, "Submitted", employee, null, [])
            : new(3, "Personal information", StageState.Current, Who("To fill in and sign", "Not submitted yet"), employee,
                "Bio data, contacts, address, next of kin, emergency contacts and bank account.", [], forEmployee));

        if (checklist is null)
            stages.Add(new(4, "Document upload", activated ? StageState.Done : StageState.Current, activated ? "Done" : "In progress",
                employee, "The documents themselves are for HR only.", []));
        else
        {
            var required = checklist.RequiredTotal;
            var uploaded = checklist.RequiredUploaded;
            var medical = checklist.Visible.FirstOrDefault(d => d.Code == "MedicalReport");
            var medicalNote = medical is null ? null : $"Medical report: {Text(medical.Status)}.";
            stages.Add(checklist.Rejected > 0
                ? new(4, "Document upload", StageState.Current, $"{checklist.Rejected} to upload again", employee,
                    Join($"{uploaded} of {required} required documents uploaded; {checklist.Rejected} rejected (see the reason on each).", medicalNote), [], forEmployee)
                : uploaded >= required
                    ? new(4, "Document upload", StageState.Done, $"{uploaded} of {required} uploaded", employee, medicalNote, [])
                    : new(4, "Document upload", StageState.Current, $"{uploaded} of {required} uploaded", employee,
                        Join(Who("Upload the rest from \"My new-hire documents\".", "Waiting for the new employee's uploads."), medicalNote), [], forEmployee));
        }

        var verifiedNote = checklist is null ? null : $"{checklist.Visible.Count(d => d.IsRequired && d.Status == EmployeeDocumentStatus.Verified)} of {checklist.RequiredTotal} required documents verified so far.";
        stages.Add(AfterVerification.Contains(status)
            ? new(5, "Recruitment Officer verification", StageState.Done, "All documents verified", "Recruitment Officer", null, [])
            : status == OnboardingStatus.AwaitingDocumentVerification
                ? new(5, "Recruitment Officer verification", StageState.Current, "Waiting for verification", "Recruitment Officer",
                    Join(verifiedNote, Who("Nothing to do: you'll be emailed if a document needs replacing.", null)), [], !forEmployee)
                : new(5, "Recruitment Officer verification", StageState.Upcoming, "Starts as documents come in", "Recruitment Officer", verifiedNote, []));

        stages.Add(activated
            ? new(6, "Staff account activation", StageState.Done, "Staff account active", "Recruitment Officer / HR Manager", null, [])
            : status == OnboardingStatus.AwaitingActivation
                ? new(6, "Staff account activation", StageState.Current, "Waiting for activation", "Recruitment Officer / HR Manager",
                    Who("Everything is in and verified. HR will now activate your staff account.", "Ready: give the staff number and activate."), [], !forEmployee)
                : new(6, "Staff account activation", StageState.Upcoming, "After verification", "Recruitment Officer / HR Manager", null, []));

        stages.Add(TaskStage(7, "IT set-up", "IT", taskList.Where(t => t.Team == OnboardingTeam.InformationTechnology), early: true, activated,
            "Email account, system access and equipment."));
        stages.Add(TaskStage(8, "Department induction", "Supervisor / Head of Department", taskList.Where(t => t.Team == OnboardingTeam.Department),
            early: false, activated, "Your department's induction, after activation."));

        stages.Add(status == OnboardingStatus.Completed
            ? new(9, "Onboarding complete", StageState.Done, Who("Welcome to GREL!", "Completed"), "HR", null, [])
            : new(9, "Onboarding complete", StageState.Upcoming, "After the induction", "HR", null, []));

        return new OnboardingJourneyDto(stages, forEmployee, status == OnboardingStatus.Completed);
    }

    private static JourneyStage TaskStage(int n, string title, string owner,
        IEnumerable<(OnboardingTeam Team, string Title, OnboardingTaskStatus Status, string? AssignedTo)> tasks, bool early, bool activated, string about)
    {
        var list = tasks.Select(t => new JourneyTask(t.Title, t.Status == OnboardingTaskStatus.Done, t.Status == OnboardingTaskStatus.NotNeeded, t.AssignedTo)).ToList();
        var finished = list.Count(t => t.Done || t.NotNeeded);
        if (list.Count > 0 && finished == list.Count) return new(n, title, StageState.Done, "All done", owner, about, list);
        var started = activated || early && finished > 0;
        return new(n, title, started ? StageState.Current : StageState.Upcoming,
            list.Count == 0 ? (activated ? "Nothing listed" : "After activation") : $"{finished} of {list.Count} done", owner, about, list);
    }

    private static string Text(EmployeeDocumentStatus s) => s switch
    {
        EmployeeDocumentStatus.Pending => "not uploaded yet",
        EmployeeDocumentStatus.Submitted => "uploaded, waiting for review",
        EmployeeDocumentStatus.UnderReview => "being reviewed",
        EmployeeDocumentStatus.Verified => "verified",
        EmployeeDocumentStatus.Rejected => "rejected: upload it again",
        _ => "not applicable"
    };

    private static string? Join(params string?[] parts)
    {
        var p = parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return p.Count == 0 ? null : string.Join(" ", p);
    }
}

/// <summary>Loads the journey for the new employee (their own onboarding only) or from the staff page's details.</summary>
public class OnboardingJourneyService(OnboardingService onboardings, DocumentVerificationService documents, IRepository<OnboardingTask> tasks)
{
    public async Task<OnboardingJourneyDto?> GetMineAsync(int applicationId, CancellationToken ct = default)
    {
        var a = await onboardings.GetMyAppointmentAsync(applicationId, ct);
        var c = a is null ? null : await documents.GetMineAsync(applicationId, ct);
        if (a is null || c is null) return null;
        var list = await tasks.ListAsync(t => t.OnboardingRecordId == a.OnboardingId, ct);
        return OnboardingJourney.Build(a.Status, a.LetterReady, a.LetterSignedAt is not null, c.PersonalRecordSigned, c,
            list.OrderBy(t => t.SortOrder).Select(t => (t.Team, t.Title, t.Status, (string?)null)), forEmployee: true);
    }

    /// <summary>The staff wording, from the details the onboarding page already loaded (no extra query).</summary>
    public static OnboardingJourneyDto ForStaff(OnboardingDetailsDto o) =>
        OnboardingJourney.Build(o.Status, o.LetterIssuedAt is not null, o.LetterSignedAt is not null, o.PersonalRecordSigned ?? o.Documents?.PersonalRecordSigned ?? false,
            o.Documents, o.Tasks.Select(t => (t.Team, t.Title, t.Status, t.AssignedTo)), forEmployee: false);
}
