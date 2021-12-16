// <copyright file="NoOpSerilogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class NoOpSerilogLogProvider : SerilogLogProvider
    {
        private static readonly IDisposable NoopDisposableInstance = new DisposableAction();

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
        protected override OpenMdc GetOpenMdcMethod()
        {
            return (_, __, ___) => NoopDisposableInstance;
        }
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    }
}
