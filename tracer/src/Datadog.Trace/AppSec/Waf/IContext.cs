// <copyright file="IContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IContext : IDisposable
    {
        IResult Run(ulong timeoutMicroSeconds);

        void AggregateAddresses(IDictionary<string, object> args);
    }
}
