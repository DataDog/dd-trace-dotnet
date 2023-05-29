// <copyright file="CachedMetadataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    internal static class CachedMetadataHelper<TMarkerType>
    {
        private static readonly Type MetadataType;

        static CachedMetadataHelper()
        {
            MetadataType = typeof(TMarkerType).Assembly.GetType("Grpc.Core.Metadata");
        }

        /// <summary>
        /// Creates a Grpc.Core.Metadata object
        /// </summary>
        public static object CreateMetadata() => Activator.CreateInstance(MetadataType);
    }
}
