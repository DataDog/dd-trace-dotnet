// <copyright file="ApplyDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal struct ApplyDetails
{
    public ApplyDetails()
    {
        Filename = null;
        ApplyState = ApplyStates.ACKNOWLEDGED;
        Error = null;
    }

    public string Filename { get; set; }

    public uint ApplyState { get; set; }

    public string Error { get; set; }

    public override string ToString()
    {
        return $"{Filename}, {ApplyState}, {Error}";
    }
}
