// <copyright file="IRemoteConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using Datadog.Trace.Internal.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.Internal.RemoteConfigurationManagement.Transport
{
    internal interface IRemoteConfigurationApi
    {
        Task<GetRcmResponse?> GetConfigs(GetRcmRequest request);
    }
}
