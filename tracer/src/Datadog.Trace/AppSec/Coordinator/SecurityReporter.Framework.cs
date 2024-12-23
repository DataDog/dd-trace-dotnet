// <copyright file="SecurityReporter.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETFRAMEWORK
using System;
using System.Runtime.CompilerServices;
using System.Web;
using Datadog.Trace.Headers;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Coordinator;

internal partial class SecurityReporter
{
    private static readonly bool? UsingIntegratedPipeline;

    static SecurityReporter()
    {
        if (UsingIntegratedPipeline == null)
        {
            try
            {
                UsingIntegratedPipeline = TryGetUsingIntegratedPipelineBool();
            }
            catch (Exception ex)
            {
                UsingIntegratedPipeline = false;
                Log.Error(ex, "Unable to query the IIS pipeline. Request and response information may be limited.");
            }
        }
    }

    internal bool CanAccessHeaders => UsingIntegratedPipeline is true or null;

    /// <summary>
    /// ! This method should be called from within a try-catch block !
    /// If the application is running in partial trust, then trying to call this method will result in
    /// a SecurityException to be thrown at the method CALLSITE, not inside the <c>TryGetUsingIntegratedPipelineBool(..)</c> method itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetUsingIntegratedPipelineBool() => HttpRuntime.UsingIntegratedPipeline;

    internal static void SafeCollectHeaders(Span internalSpan, bool isRoot)
    {
        var context = HttpContext.Current;
        var securityReporter = new SecurityReporter(internalSpan, new SecurityCoordinator.HttpTransport(context), isRoot);
        if (context != null)
        {
            var headers = new NameValueHeadersCollection(context.Request.Headers);
            securityReporter.AddRequestHeaders(headers);
        }
    }
}
#endif
