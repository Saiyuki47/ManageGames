namespace ManageGames.Auth;

/// <summary>Browser security headers for every response (OWASP Secure Headers Project).</summary>
public static class SecurityHeaders
{
    // Everything is served from the app itself and there are no inline scripts or styles, so the policy
    // can be strict. data: images are needed by Bootstrap's CSS (e.g. the navbar toggler icon).
    public const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            // Older browsers that don't know frame-ancestors (the antiforgery system would add SAMEORIGIN otherwise).
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "same-origin";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            await next(context);
        });
    }
}
