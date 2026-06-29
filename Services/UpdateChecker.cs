using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;

namespace HomeAccounting.Services;

public static class UpdateChecker
{
    const string ReleasesApi  = "https://api.github.com/repos/andrey1b/HomeAccounting/releases/latest";
    const string ReleasesPage = "https://github.com/andrey1b/HomeAccounting/releases/latest";

    /// <summary>onDone(tag, error): tag != null — найдено обновление; error=true — не удалось проверить (нет сети).</summary>
    public static void CheckAsync(Action<string?, bool> onDone)
    {
        Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(8);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HomeAccounting-Updater/1.0");
                // обход кэша CDN GitHub, чтобы сразу видеть свежий релиз
                http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
                var url = $"{ReleasesApi}?_={DateTime.UtcNow.Ticks}";

                var release = await http.GetFromJsonAsync<GitHubRelease>(url);
                if (release?.TagName is null) { onDone(null, false); return; }

                var remoteVer = Version.Parse(release.TagName.TrimStart('v', 'V'));
                var localVer  = Normalize(Assembly.GetExecutingAssembly().GetName().Version!);

                onDone(remoteVer > localVer ? release.TagName : null, false);
            }
            catch { onDone(null, true); }
        });
    }

    // Приводим к 3-компонентному виду (Revision=0), чтобы 4.4.5 корректно сравнивалось с 4.4.5.0
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    public static void OpenReleasesPage() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReleasesPage)
            { UseShellExecute = true });

    private record GitHubRelease(string? TagName, string? Name);
}
