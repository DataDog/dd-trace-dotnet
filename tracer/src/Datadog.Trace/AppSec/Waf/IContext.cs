// <copyright file="IContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
#nullable enable

namespace Datadog.Trace.AppSec.Waf;

internal interface IContext : IDisposable
{
    IResult? Run(IDictionary<string, object> addressData, ulong timeoutMicroSeconds);

    IResult? RunWithEphemeral(IDictionary<string, object> ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp);
}
