namespace Grsms.Web.Security;

/// <summary>
/// Security headers sent with every response (Phase 12). See docs/04-security-and-roles.md, "Web protections".
///
/// The Content-Security-Policy tells the browser to run scripts, load styles, fonts and images ONLY from GRSMS itself:
/// every library is served from wwwroot/lib and there is no inline script anywhere (page behaviour is in js/site.js, wired
/// with data-* attributes). So even if someone managed to slip a script into a page, the browser would refuse to run it.
///
/// Inline style attributes (style="width: 40%" on chart bars, progress bars, ...) are allowed: they cannot run code,
/// and the alternative attribute-only rule (style-src-attr) is not supported by every browser GREL may use.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>The policy. Microsoft's sign-in page is allowed as a form target for "Sign in with Microsoft 365".</summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self' https://login.microsoftonline.com";

    public static IApplicationBuilder UseGrsmsSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var h = context.Response.Headers;
        h["Content-Security-Policy"] = ContentSecurityPolicy;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";                                   // older browsers; frame-ancestors above for new ones
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        h["Cross-Origin-Opener-Policy"] = "same-origin";                 // other sites' windows cannot reach into GRSMS
        h["X-Permitted-Cross-Domain-Policies"] = "none";
        await next();
    });
}
