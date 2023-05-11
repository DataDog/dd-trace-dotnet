// <copyright file="RumRequestInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec.Waf.Initialization;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#endif

namespace Datadog.Trace.Rum;

internal sealed class RumRequestInjector
{
    private static readonly Lazy<RumRequestInjector> _instance = new(() => new RumRequestInjector());

    private RumLibraryInvoker? _rumLibraryInvoker;
    private LibraryInitializationResult? _libraryInitializationResult;

    // Private constructor to prevent direct instantiation
    private RumRequestInjector()
    {
        // Initialize the shared library
        if (_libraryInitializationResult == null)
        {
            _libraryInitializationResult = RumLibraryInvoker.Initialize();
            if (!_libraryInitializationResult.Success)
            {
                Enabled = false;
                // logs happened during the process of initializing
                return;
            }

            _rumLibraryInvoker = _libraryInitializationResult.RumLibraryInvoker;
        }
    }

    internal bool Enabled { get; private set; }

    // Public property to access the singleton instance
    public static RumRequestInjector Instance => _instance.Value;

    public static void UpdateStreamResponseBody(HttpContext httpContext)
    {
        // Store the current response body stream into item of HttpContext
        httpContext.Items["DD_RUM_ResponseBodyStream"] = httpContext.Response.Body;

        // Create a new MemoryStream to replace the current response body stream
        var memoryStream = new MemoryStream();
        httpContext.Response.Body = memoryStream;

        // Add a callback to inject the script into the response body
        httpContext.Response.OnStarting(async () => await Instance.Inject(httpContext).ConfigureAwait(false));
    }

    // Only edit response of status code:
    // - 200
    private static bool IsRequestHandled(HttpContext httpContext)
    {
        return httpContext.Response.StatusCode == 200;
    }

    // Only edit response of types:
    // - text/html
    private static bool IsValidContentType(HttpContext httpContext)
    {
        return httpContext.Response.ContentType == "text/html";
    }

    // Get the html of the response body
    private static async Task<string> GetHtml(HttpContext httpContext)
    {
        var memoryStream = (MemoryStream)httpContext.Response.Body;

        memoryStream.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(memoryStream).ReadToEndAsync().ConfigureAwait(false);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return body;
    }

    private async Task Inject(HttpContext httpContext)
    {
        if (!IsRequestHandled(httpContext) || !IsValidContentType(httpContext))
        {
            // Do not inject if the request is not handled or the content type is not valid
            return;
        }

        // Get the html of the response body
        var html = await GetHtml(httpContext).ConfigureAwait(false);

        // Restore the original response body stream
        httpContext.Response.Body = (Stream)httpContext.Items["DD_RUM_ResponseBodyStream"];

        RumInjectionPointStruct pointStruct = new();
        // Scan the html using the shared library
        var status = Scan(html, ref pointStruct);
        if (status != RumInjectionPointStatus.FOUND_STOP)
        {
            // No injection point found
            return;
        }

        var match_rule = Marshal.PtrToStringAuto(pointStruct.MatchRule);

        var stringToInject = @"<script
            src=""https://www.datadoghq-browser-agent.com/us1/v4/datadog-rum.js""
                type=""text/javascript"">
                    </script>
                    <script>
                    window.DD_RUM && window.DD_RUM.init({
                    clientToken: 'pub426331ddcec4d0e207b237aed0acd474',
                    applicationId: 'd511fe75-d5cf-4fa0-9a68-bc8ab8dd1ccc',
                    site: 'datadoghq.com',
                    service: 'test-app-flavien',
                    env: 'flavien',
                    // Specify a version number to identify the deployed version of your application in Datadog 
                    // version: '1.0.0',
                    sessionSampleRate: 100,
                    sessionReplaySampleRate: 20,
                    trackUserInteractions: true,
                    trackResources: true,
                    trackLongTasks: true,
                    defaultPrivacyLevel: 'mask-user-input',
                });
            window.DD_RUM &&
                window.DD_RUM.startSessionReplayRecording();
         </script>";

        // Insert the script at the good position
        var result = html.Insert(pointStruct.Position, stringToInject);

        // Add the changes to the response body
        var bytes = Encoding.UTF8.GetBytes(result);
        await httpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
    }

    // Scan the html using the shared library
    private RumInjectionPointStatus Scan(string html, ref RumInjectionPointStruct pointStruct)
    {
        if (_rumLibraryInvoker == null)
        {
            return RumInjectionPointStatus.ABORT;
        }

        return _rumLibraryInvoker.Scan(html, ref pointStruct);
    }
}
#endif
