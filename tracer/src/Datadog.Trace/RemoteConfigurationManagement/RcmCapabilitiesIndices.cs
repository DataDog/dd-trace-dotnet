// <copyright file="RcmCapabilitiesIndices.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal static class RcmCapabilitiesIndices
    {
        public const int Reserved = 0;
        public const int AsmActivation = 1;
        public const int AsmIpBlocking = 1 << 2;
        public const int AsmDdRules = 1 << 3;
    }
}
