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
    internal class CoreHttpContextStore
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CoreHttpContextStore>();

        public static readonly CoreHttpContextStore Instance = new();

        private readonly AsyncLocal<HttpContext?> _localStore = new();

        public HttpContext? Get()
        {
            if (_localStore.Value is null)
            {
                Log.Debug("CoreHttpContextStore.Get called but returning null for HttpContext");
            }

            return _localStore.Value;
        }

        public void Set(HttpContext context) => _localStore.Value = context;

        public void Remove() => _localStore.Value = null;
    }
}

#endif
