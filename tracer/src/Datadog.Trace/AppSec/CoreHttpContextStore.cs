// <copyright file="CoreHttpContextStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec
{
    internal sealed class CoreHttpContextStore
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CoreHttpContextStore>();

        public static readonly CoreHttpContextStore Instance = new();

        // The HttpContext is held behind an indirection rather than stored in the AsyncLocal directly.
        // An AsyncLocal write is only visible to the current ExecutionContext and to the ones created
        // afterwards, so writing null in Remove() would leave every context captured during the request
        // (a customer's Task.Run, a fire and forget call, an OnCompleted callback...) still pointing at an
        // HttpContext that ASP.NET Core has since uninitialized and handed back to its pool. Clearing a
        // field of a shared holder instead is visible everywhere the holder was captured, so the reference
        // dies with the request. This is why ASP.NET Core's own HttpContextAccessor is written this way.
        private readonly AsyncLocal<HttpContextHolder> _localStore = new();

        public HttpContext? Get()
        {
            var context = _localStore.Value?.Context;
            if (context is null)
            {
                Log.Debug("CoreHttpContextStore.Get called but returning null for HttpContext");
            }

            return context;
        }

        // a fresh holder per request, so that a holder still captured by a previous request can never be
        // revived and start handing out this request's context
        public void Set(HttpContext context) => _localStore.Value = new HttpContextHolder(context);

        public void Remove() => _localStore.Value?.Clear();

        private sealed class HttpContextHolder
        {
            private HttpContext? _context;

            public HttpContextHolder(HttpContext context) => _context = context;

            // the holder is shared by every ExecutionContext captured during the request, so it can be read
            // from a thread other than the one that clears it
            public HttpContext? Context => Volatile.Read(ref _context);

            public void Clear() => Interlocked.Exchange(ref _context, null);
        }
    }
}

#endif
