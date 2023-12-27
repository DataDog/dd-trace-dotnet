using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
#if IS_WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif
using NuGet.Packaging;
using Nuke.Common;
using Nuke.Common.Execution;

public class WindowsBuildFinishedNotificationAttribute : BuildExtensionAttributeBase, IOnBuildFinished
{
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string BuildImages = Path.Combine(AppData, "build-images");
    private static readonly string SucceedImage = Path.Combine(BuildImages, "build-succeed.jpg");
    private static readonly string FailedImage = Path.Combine(BuildImages, "build-failed.jpg");
    private static readonly string LogoImage = Path.Combine(BuildImages, "dd_logo.png");

    public void OnBuildFinished(NukeBuild build)
    {
#if IS_WINDOWS
        if (!(bool.TryParse(Environment.GetEnvironmentVariable("NUKE_NOTIFY"), out var notify)
            && notify))
        {
            return;
        }

        CreateLocalImages();

        var message =
            build.IsSuccessful
                ? $"Build succeeded on {DateTime.Now.ToString(CultureInfo.CurrentCulture)}. ＼（＾ᴗ＾）／"
                : $"Build failed on {DateTime.Now.ToString(CultureInfo.CurrentCulture)}. (╯°□°）╯︵ ┻━┻";

        var image =
            build.IsSuccessful
                ? SucceedImage
                : FailedImage;

        new ToastContentBuilder()
            .AddInlineImage(new Uri($"file:///{image}"))
            .AddAppLogoOverride(new Uri($"file:///{LogoImage}"), ToastGenericAppLogoCrop.Circle)
            .AddText(message)
            .Show();
#endif
    }

    private static void CreateLocalImages()
    {
        if (!Directory.Exists(BuildImages))
        {
            Directory.CreateDirectory(BuildImages);
        }

        var urlBase = "https://github.com/robertpi/build-images/blob/main/";
        var tasks = new []
        {
            DownloadAndCache(urlBase + "build-succeed.jpg?raw=true", SucceedImage),
            DownloadAndCache(urlBase + "build-failed.jpg?raw=true", FailedImage),
            DownloadAndCache(urlBase + "dd_logo.jpg?raw=true", LogoImage),
        };

        Task.WhenAll(tasks).Wait();
    }

    static async Task DownloadAndCache(string url, string target)
    {
        if (!File.Exists(target))
        {
            var wc = new HttpClient();
            await using var stream = await wc.GetStreamAsync(url);
            stream.CopyToFile(target);
        }
    }
}

