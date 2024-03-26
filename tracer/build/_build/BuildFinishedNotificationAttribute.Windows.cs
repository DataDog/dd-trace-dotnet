
#if NUKE_NOTIFY
#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using NuGet.Packaging;
using Nuke.Common;
using Nuke.Common.Execution;
using Logger = Serilog.Log;

public partial class BuildFinishedNotificationAttribute
{
    private static readonly bool Enabled = false;

    private static readonly Uri? SucceedImage = null;
    private static readonly Uri? FailedImage = null;
    private static readonly Uri? LogoImage = null;

    private static bool resourcesAvailable = false;

    static BuildFinishedNotificationAttribute()
    {
        try
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUKE_NOTIFY"), out var notify))
            {
                Enabled = notify;
            }

            if (Uri.TryCreate(Environment.GetEnvironmentVariable("NUKE_NOTIFY_RESOURCES"), UriKind.Absolute, out var resourceSource))
            {
                Uri buildImages;
                if (resourceSource.IsFile)
                {
                    buildImages = resourceSource;
                }
                else
                {
                    var appDataBuildImages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "build-images");
                    buildImages = new Uri(appDataBuildImages);
                }

                SucceedImage = new Uri(buildImages, "build-succeed.jpg");
                FailedImage = new Uri(buildImages, "build-failed.jpg");
                LogoImage = new Uri(buildImages, "dd_logo.png");

                if (!Directory.Exists(buildImages.LocalPath))
                {
                    Directory.CreateDirectory(buildImages.LocalPath);
                }

                if (!resourceSource.IsFile)
                {
                    var tasks = new []
                    {
                        DownloadAndCache(new Uri(resourceSource, "build-succeed.jpg?raw=true"), SucceedImage),
                        DownloadAndCache(new Uri(resourceSource, "build-failed.jpg?raw=true"), FailedImage),
                        DownloadAndCache(new Uri(resourceSource, "dd_logo.jpg?raw=true"), LogoImage),
                    };

                    Task.WhenAll(tasks).Wait();
                }

                resourcesAvailable =
                    File.Exists(SucceedImage.LocalPath) &&
                    File.Exists(FailedImage.LocalPath) &&
                    File.Exists(LogoImage.LocalPath);
            }
        }
        catch
        {
            Enabled = false;
            Logger.Error("Failed to initialize notifications");
        }
    }

    public void OnBuildFinished(NukeBuild build)
    {
        if (!Enabled)
        {
            return;
        }

        var message =
            build.IsSuccessful
                ? $"Build succeeded on {DateTime.Now.ToString(CultureInfo.CurrentCulture)}. ＼（＾ᴗ＾）／"
                : $"Build failed on {DateTime.Now.ToString(CultureInfo.CurrentCulture)}. (╯°□°）╯︵ ┻━┻";

        var builder = new ToastContentBuilder();

        if (resourcesAvailable)
        {
            var image =
                build.IsSuccessful
                    ? SucceedImage
                    : FailedImage;

            builder =
                builder
                    .AddInlineImage(image)
                    .AddAppLogoOverride(LogoImage, ToastGenericAppLogoCrop.Circle);
        }

        builder
            .AddText(message)
            .Show();
    }


    static async Task DownloadAndCache(Uri url, Uri target)
    {
        if (!File.Exists(target.LocalPath))
        {
            var wc = new HttpClient();
            await using var stream = await wc.GetStreamAsync(url);
            stream.CopyToFile(target.LocalPath);
        }
    }
}
#endif