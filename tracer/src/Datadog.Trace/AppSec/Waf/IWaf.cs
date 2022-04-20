// <copyright file="IWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Waf.ReturnTypesManaged;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IWaf : IDisposable
    {
        public Version Version { get; }

        public bool InitializedSuccessfully { get; }

        public InitializationResult InitializationResult { get; }

        public IContext CreateContext();
    }
}
