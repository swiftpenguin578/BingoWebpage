namespace Bingo.BrowserTests;

public sealed class TransientToastUiTests
{
    [Fact]
    public void AllSharedLayoutsRenderTheSameTransientToastPartial()
    {
        var root = FindRepositoryRoot();
        var shared = Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared");
        var partial = File.ReadAllText(Path.Combine(shared, "_TransientToast.cshtml"));

        foreach (var layoutName in new[] { "_Layout.cshtml", "_AdminLayout.cshtml", "_AdminOverlayLayout.cshtml" })
        {
            var layout = File.ReadAllText(Path.Combine(shared, layoutName));
            Assert.Contains("@await Html.PartialAsync(\"_TransientToast\")", layout);
            Assert.DoesNotContain("data-dismiss-notice", layout);
        }

        Assert.Equal(4, partial.Split("<svg class=\"app-toast-icon\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("UiMessageType.Success", partial);
        Assert.Contains("UiMessageType.Warning", partial);
        Assert.Contains("UiMessageType.Error", partial);
        Assert.Contains("default:", partial);
        Assert.Contains("data-transient-toast", partial);
        Assert.Contains("data-toast-duration=\"6000\"", partial);
        Assert.Contains("admin-route-dialog-close", partial);
        Assert.DoesNotContain("data-feedback-target", partial);
    }

    [Fact]
    public void TransientToastKeepsTheSharedTypeAndPauseContract()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.public-ui.css"));
        var layoutStyles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.transitional.application.css"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var noticeRegionStyles = layoutStyles[layoutStyles.IndexOf("#app-notice-region", StringComparison.Ordinal)..];

        Assert.Contains("display: grid", noticeRegionStyles);
        Assert.Contains("gap: 0.5rem", noticeRegionStyles);
        Assert.Contains("position: fixed", noticeRegionStyles);
        Assert.Contains("right: 1rem", noticeRegionStyles);
        Assert.Contains("bottom: 1rem", noticeRegionStyles);
        Assert.Contains("z-index: 2147483000", noticeRegionStyles);
        Assert.Contains(".app-toast-success .app-toast-icon {", styles);
        Assert.Contains(".app-toast-warning .app-toast-icon {", styles);
        Assert.Contains(".app-toast-error .app-toast-icon {", styles);
        Assert.Contains(".app-toast-information .app-toast-icon {", styles);
        var toastStyles = styles[styles.IndexOf(".app-toast {", StringComparison.Ordinal)..styles.IndexOf(".app-toast-icon", StringComparison.Ordinal)];
        Assert.Contains("background: var(--public-ui-charcoal-surface);", toastStyles);
        Assert.Contains("color: rgba(245, 245, 245, 0.9);", toastStyles);
        Assert.Contains("border: 0;", toastStyles);
        Assert.Contains("box-shadow: none;", toastStyles);
        Assert.Contains(".app-toast-success .app-toast-icon { color:", styles);
        Assert.Contains(".app-toast-warning .app-toast-icon { color:", styles);
        Assert.Contains(".app-toast-error .app-toast-icon { color:", styles);
        Assert.Contains(".app-toast-information .app-toast-icon { color:", styles);
        Assert.Contains(".app-toast-icon {", styles);
        Assert.Contains("width: 1.15rem", styles);
        Assert.Contains("height: 1.15rem", styles);
        Assert.Contains("flex: 0 0 1.15rem", styles);
        Assert.Contains("transition: opacity 140ms ease-out", styles);
        Assert.Contains(".app-toast.is-dismissing { opacity: 0; }", styles);
        Assert.Contains("prefers-reduced-motion: reduce", styles);
        Assert.Contains("toast.dataset.toastDuration = \"6000\"", script);
        Assert.Contains("toast.classList.add(\"is-dismissing\")", script);
        Assert.Contains("matchMedia?.(\"(prefers-reduced-motion: reduce)\")", script);
        Assert.Contains("mouseenter", script);
        Assert.Contains("mouseleave", script);
        Assert.Contains("focusin", script);
        Assert.Contains("focusout", script);
        Assert.Contains("window.clearTimeout(toast._toastTimer)", script);
        Assert.Contains("data-dismiss-toast", script);
        Assert.DoesNotContain("const feedback = document.querySelector(\"[data-feedback-target]", script);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
