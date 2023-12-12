// <copyright file="SpanExtensions.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    public static partial class SpanExtensions
    {
        private static void RunBlockingCheck(Span span, string userId)
        {
            var security = Security.Instance;

            if (security.Enabled && AspNetCoreAvailabilityChecker.IsAspNetCoreAvailable())
            {
                RunBlockingCheckUnsafe(security, span, userId);
            }

            // Don't inline this, so we don't load the aspnetcore types if they're not available
            [MethodImpl(MethodImplOptions.NoInlining)]
            static void RunBlockingCheckUnsafe(Security security, Span span, string userId)
            {
                if (CoreHttpContextStore.Instance.Get() is { } httpContext)
                {
                    security.CheckUser(httpContext, span, userId);
                }
            }
        }
    }
}
#endif
