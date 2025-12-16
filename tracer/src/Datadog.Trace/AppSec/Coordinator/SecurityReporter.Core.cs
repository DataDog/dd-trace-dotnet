// <copyright file="SecurityReporter.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System.Runtime.CompilerServices;
using Datadog.Trace.Headers;

namespace Datadog.Trace.AppSec.Coordinator;

internal sealed partial class SecurityReporter
{
    private bool CanAccessHeaders => true;

    /// <summary>
    /// Outside of a web context this can't work and there are no web assemblies to load so without the no inlining, this would cause a load assembly exception
    /// </summary>
    /// <param name="span">the span to report on</param>
    /// <param name="searchRootSpan">should we fetch the root span for you</param>
    internal static void SafeCollectHeaders(Span span, bool searchRootSpan = true)
    {
        if (AspNetCoreAvailabilityChecker.IsAspNetCoreAvailable())
        {
            CollectHeadersSafe(searchRootSpan);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void CollectHeadersSafe(bool searchRootSpanImpl)
        {
            var context = CoreHttpContextStore.Instance.Get();
            if (context is not null)
            {
                var securityReporter = new SecurityReporter(span, new SecurityCoordinator.HttpTransport(context), !searchRootSpanImpl);
                securityReporter.CollectHeaders();
            }
        }
    }

    internal void CollectHeaders()
    {
        var headers = new HeadersCollectionAdapter(_httpTransport.Context.Request.Headers);
        AddRequestHeaders(headers);
    }
}
#endif
