using System.Diagnostics;

namespace GetSetGo.Web.Services;

internal static class DevelopmentBrowserLauncher
{
    public static void Register(WebApplication app)
    {
        // Opt in only through the local launch profile, never on a hosted server.
        if (!app.Configuration.GetValue<bool>("Development:OpenBrowserTabs") ||
            !OperatingSystem.IsWindows())
            return;

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Urls.FirstOrDefault(url => url.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase))
                ?? app.Urls.FirstOrDefault(url => url.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase));
            if (address is null)
            {
                app.Logger.LogWarning("No localhost address is available for opening the development browser tabs.");
                return;
            }

            foreach (var path in new[] { "/", "/swagger" })
            {
                var url = address.TrimEnd('/') + path;
                try
                {
                    using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception exception)
                {
                    app.Logger.LogWarning(exception, "Could not open {Url}. Open it manually in your browser.", url);
                }
            }
        });
    }
}
