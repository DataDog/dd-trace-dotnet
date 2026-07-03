// <copyright file="FlowMethodMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowMethodMetadata
    {
        public FlowMethodMetadata(int methodMetadataIndex, string displayName)
        {
            MethodMetadataIndex = methodMetadataIndex;
            DisplayName = displayName;
        }

        public int MethodMetadataIndex { get; }

        public string DisplayName { get; }
    }
}
