// <copyright file="ApplyState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol;

internal enum ApplyState : uint
{
    UNACKNOWLEDGED = 1,
    ACKNOWLEDGED = 2,
    ERROR = 3,
}
