using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Bingo.AdminDesignParityFixture;

// Test-host only: observe the actual localizer boundary, including variable keys,
// partials, page-model Localize calls and labels serialized for JavaScript.
internal sealed class DanishRenderAuditFactory(IStringLocalizerFactory inner, IHttpContextAccessor accessor) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new Audit(inner.Create(resourceSource), accessor, resourceSource.Name);
    public IStringLocalizer Create(string baseName, string location) => new Audit(inner.Create(baseName, location), accessor, baseName);
    private sealed class Audit(IStringLocalizer inner, IHttpContextAccessor accessor, string resource) : IStringLocalizer
    {
        public LocalizedString this[string name] => Record(inner[name]);
        public LocalizedString this[string name, params object[] arguments] => Record(inner[name, arguments]);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
        private LocalizedString Record(LocalizedString value)
        {
            if (value.ResourceNotFound && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "da" && accessor.HttpContext is { } context)
            {
                if (context.Items[typeof(DanishRenderAuditFactory)] is not HashSet<string> missing)
                    context.Items[typeof(DanishRenderAuditFactory)] = missing = [];
                missing.Add(resource + ": " + value.Name);
            }
            return value;
        }
    }
}
internal sealed class DanishRenderAuditStartup : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, continuation) =>
        {
            context.Response.OnStarting(() =>
            {
                var missing = context.Items[typeof(DanishRenderAuditFactory)] as HashSet<string> ?? [];
                context.Response.Headers["X-Parity-Danish-Missing"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(missing.Order())));
                return Task.CompletedTask;
            });
            return continuation(context);
        });
        next(app);
    };
}
