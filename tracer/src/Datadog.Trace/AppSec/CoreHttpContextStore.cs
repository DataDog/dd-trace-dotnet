// <copyright file="CoreHttpContextStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec
{
    internal class CoreHttpContextStore
    {
        public static readonly CoreHttpContextStore Instance = new();

        private AsyncLocal<HttpContext> localStore = new();

        public HttpContext Get() => localStore.Value;

        public void Set(HttpContext context) => localStore.Value = context;
    }
}

#endif
