// <copyright file="GetRcmRequestExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    internal static class GetRcmRequestExtensions
    {
        internal static bool Matches(this GetRcmRequest request, GetRcmResponse response)
        {
            var requestBackendClientState = request.Client.State.BackendClientState;
            var responseBackendClientstate = response.Targets.Signed.Custom.OpaqueBackendState;
            return requestBackendClientState == responseBackendClientstate;
        }
    }
}
