// <copyright file="RumRequestInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using System.IO;
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

    public void InjectRumRequest(HttpContext httpContext)
    {
        httpContext.Response.OnStarting(async () => await Inject().ConfigureAwait(false));
        async Task Inject()
        {
            if (!IsRequestHandled(httpContext) || !IsValidContentType(httpContext))
            {
                // Do not inject if the request is not handled or the content type is not valid
                return;
            }

            // Get the html of the response body
            var html = await GetHtml(httpContext).ConfigureAwait(false);

            // Scan the html using the shared library
            var scanResult = Scan(ref html);

            // Add <script>alert(1)</script> to the response body of httpContext
            var bytes = Encoding.UTF8.GetBytes("<script>alert(1)</script>");
            await httpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
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
        var originalResponseBody = httpContext.Response.Body;

        try
        {
            using var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;

            // Read the content of the memory stream
            using var streamReader = new StreamReader(memoryStream, Encoding.UTF8);
            string htmlContent = await streamReader.ReadToEndAsync().ConfigureAwait(false);

            // Write the content back to the original response body
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalResponseBody).ConfigureAwait(false);

            return htmlContent;
        }
        finally
        {
            // Restore the original response body
            httpContext.Response.Body = originalResponseBody;
        }
    }

    // Scan the html using the shared library
    private RumScanResultStruct Scan(ref string? html)
    {
        RumScanResultStruct retNative = default;
        if (_rumLibraryInvoker == null)
        {
            return retNative;
        }

        RumScanStatus status = _rumLibraryInvoker.Scan(ref retNative, ref html);
        if (status != RumScanStatus.SCAN_MATCH)
        {
            // logs happened during the process of scanning
            return retNative;
        }

        return retNative;
    }
}
#endif
