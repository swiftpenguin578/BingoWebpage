namespace Bingo.Web.Navigation;

// A navigation preference only; callers must validate the ID against current access.
public static class AdminEventSession
{
    public const string CookieName = "Bingo.AdminEvent";
    private static CookieOptions Options(HttpContext context) => new()
    {
        HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, Path = "/",
        Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() || context.Request.IsHttps
        // No Expires/MaxAge: the preference ends with the browser session.
    };
    public static void Remember(HttpContext context, Guid eventId) =>
        context.Response.Cookies.Append(CookieName, eventId.ToString("D"), Options(context));
    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, Options(context));
}
