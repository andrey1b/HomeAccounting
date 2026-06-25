using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Windows;

namespace HomeAccounting.Services;

public static class UpdateChecker
{
    const string ReleasesApi = "https://api.github.com/repos/andrey1b/HomeAccounting/releases/latest";
    const string ReleasesPage = "https://github.com/andrey1b/HomeAccounting/releases/latest";

    public static void CheckAsync(Action<string> onUpdateFound)
    {
        Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(8);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HomeAccounting-Updater/1.0");

                var release = await http.GetFromJsonAsync<GitHubRelease>(ReleasesApi);
                if (release?.TagName is null) return;

                var remoteVer = Version.Parse(release.TagName.TrimStart('v'));
                var localVer  = Assembly.GetExecutingAssembly().GetName().Version!;

                if (remoteVer > localVer)
                    onUpdateFound(release.TagName);
            }
            catch { /* нет сети — молча пропускаем */ }
        });
    }

    public static void OpenReleasesPage() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReleasesPage)
            { UseShellExecute = true });

    private record GitHubRelease(string? TagName, string? Name);
}
