using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Grsms.Web.Security;

/// <summary>
/// Rate-limit policy names and the "too many attempts" answer (Phase 12). The limits themselves are registered in
/// Program.cs and put on actions with <c>[EnableRateLimiting(RateLimits.SignIn)]</c>.
/// </summary>
public static class RateLimits
{
    /// <summary>Sign-in, registration and password pages: Security:SignInAttemptsPerMinute posts per minute per address.</summary>
    public const string SignIn = "sign-in";

    /// <summary>Answers 429 with a short page asking the person to wait, and logs the address (it may be an attack).</summary>
    public static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? (int)Math.Ceiling(retryAfter.TotalSeconds) : 60;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = wait.ToString();
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Grsms.Security")
            .LogWarning("Too many sign-in / password requests from {IpAddress} to {Path}; refused for {Seconds} s",
                http.Connection.RemoteIpAddress, http.Request.Path, wait);
        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync(
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Too many attempts · GREL Recruitment</title>" +
            "<link rel=\"stylesheet\" href=\"/lib/bootstrap/dist/css/bootstrap.min.css\"></head><body class=\"p-5\">" +
            "<h1 class=\"h4\">Too many attempts</h1><p>For your security, please wait about " + WebUtility.HtmlEncode(wait.ToString()) +
            " seconds and try again.</p><p><a href=\"/Account/Login\">Back to sign in</a></p></body></html>", ct);
    }
}
