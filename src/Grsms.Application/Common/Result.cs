namespace Grsms.Application.Common;

/// <summary>
/// The outcome of a business operation: either it succeeded, or it failed with one or more
/// human-readable error messages (e.g. "A department with code HSE already exists.").
///
/// Services return a Result instead of throwing exceptions for normal validation problems,
/// so controllers can simply show the messages on the form.
/// </summary>
public class Result
{
    /// <summary>True when the operation completed.</summary>
    public bool Succeeded { get; }

    /// <summary>Messages to show the user when <see cref="Succeeded"/> is false.</summary>
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToList();
    }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, []);

    /// <summary>Creates a failed result with the given messages.</summary>
    public static Result Failure(params string[] errors) => new(false, errors);

    /// <summary>Creates a successful result that also carries a value (e.g. the new record's id).</summary>
    public static Result<T> Success<T>(T value) => new(value, true, []);

    /// <summary>Creates a failed result for an operation that would have returned a value.</summary>
    public static Result<T> Failure<T>(params string[] errors) => new(default, false, errors);
}

/// <summary>A <see cref="Result"/> that also carries a value when it succeeds.</summary>
public class Result<T> : Result
{
    /// <summary>The value produced by the operation. Only meaningful when <see cref="Result.Succeeded"/> is true.</summary>
    public T? Value { get; }

    internal Result(T? value, bool succeeded, IEnumerable<string> errors) : base(succeeded, errors)
    {
        Value = value;
    }
}
