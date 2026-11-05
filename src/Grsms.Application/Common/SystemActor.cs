namespace Grsms.Application.Common;

/// <summary>
/// Marks work the SYSTEM does on its own inside someone's web request, so the audit trail records it as the system and
/// not as that person. Example: the ATS screens an application while the applicant's "Apply" request is still running.
/// Without this, the rejection would be recorded under the applicant's name and could appear in their own audit trail
/// before HR has decided.
///
///   using (SystemActor.Begin("ATS (automatic screening)")) { ... save ... }
///
/// The audit interceptor (Infrastructure/Persistence/AuditSaveChangesInterceptor.cs) reads <see cref="Current"/>: while
/// it is set, audit rows get this name, no user id and no IP address. It flows with async code and ends with the block.
/// </summary>
public static class SystemActor
{
    private static readonly AsyncLocal<string?> current = new();

    /// <summary>The system actor's name while a <see cref="Begin"/> block is running, otherwise null.</summary>
    public static string? Current => current.Value;

    public static IDisposable Begin(string name)
    {
        var previous = current.Value;
        current.Value = name;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}
