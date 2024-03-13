// <copyright file="IMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    /// <summary>
    /// Duck type for Metadata
    /// Interface, as need to call methods on it
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core.Api/Metadata.cs
    /// </summary>
    internal interface IMetadata
    {
        public bool IsReadOnly { get; }

        public int Count { get; }

        public object? Get(string key);

        public IEnumerable GetAll(string key);

        public void Add(string key, string value);

        public void Add(object entry);

        public bool Remove(object entry);

        public IEnumerator GetEnumerator();
    }
}
