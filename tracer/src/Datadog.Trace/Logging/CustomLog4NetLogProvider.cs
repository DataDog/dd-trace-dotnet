// <copyright file="CustomLog4NetLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomLog4NetLogProvider : Log4NetLogProvider
    {
        private static readonly IDisposable NoopDisposableInstance = new DisposableAction();

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
        protected override OpenMdc GetOpenMdcMethod()
        {
            // Make this log provider a no-op so the automatic instrumentation does all the work
            return (_, __, ___) => NoopDisposableInstance;
        }
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    }
}
