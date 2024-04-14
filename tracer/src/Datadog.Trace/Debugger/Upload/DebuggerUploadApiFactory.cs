// <copyright file="DebuggerUploadApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger.Upload
{
    internal static class DebuggerUploadApiFactory
    {
        internal static IBatchUploadApi CreateSnapshotUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            return SnapshotUploadApi.Create(apiRequestFactory, discoveryService, gitMetadataTagsProvider);
        }

        internal static IBatchUploadApi CreateDiagnosticsUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            return DiagnosticsUploadApi.Create(apiRequestFactory, discoveryService, gitMetadataTagsProvider);
        }

        internal static IBatchUploadApi CreateSymbolsUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, IGitMetadataTagsProvider gitMetadataTagsProvider, string serviceName)
        {
            return SymbolUploadApi.Create(apiRequestFactory, discoveryService, gitMetadataTagsProvider, serviceName);
        }
    }
}
