// <copyright file="IJobFilterCollectionProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire
{
    /// <summary>
    /// DuckTyping interface for Hangfire.Common.JobFilterCollection
    /// </summary>
    /// <remarks>
    /// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Common/JobFilterCollection.cs
    /// </remarks>
    internal interface IJobFilterCollectionProxy : IDuckType
    {
        /// <summary>
        /// Calls method: System.Void Hangfire.Common.JobFilterCollection::Add(System.Object)
        /// </summary>
        void Add(object filter);

        /// <summary>
        /// Calls method: System.Void Hangfire.Common.JobFilterCollection::Add(System.Object,System.Int32)
        /// </summary>
        void Add(object filter, int order);

        /// <summary>
        /// Calls method: System.Void Hangfire.Common.JobFilterCollection::AddInternal(System.Object,System.Nullable`1[System.Int32])
        /// </summary>
        void AddInternal(object filter, int? order);

        /// <summary>
        /// Calls method: System.Void Hangfire.Common.JobFilterCollection::Clear()
        /// </summary>
        void Clear();

        /// <summary>
        /// Calls method: System.Boolean Hangfire.Common.JobFilterCollection::Contains(System.Object)
        /// </summary>
        bool Contains(object filter);

        /// <summary>
        /// Calls method: System.Collections.Generic.IEnumerator`1[Hangfire.Common.JobFilter] Hangfire.Common.JobFilterCollection::GetEnumerator()
        /// </summary>
        object GetEnumerator();

        /// <summary>
        /// Calls method: System.Void Hangfire.Common.JobFilterCollection::ValidateFilterInstance(System.Object)
        /// </summary>
        void ValidateFilterInstance(object instance);
    }
}
