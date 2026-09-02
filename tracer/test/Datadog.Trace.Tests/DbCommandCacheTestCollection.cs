// <copyright file="DbCommandCacheTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests
{
    /// <summary>
    /// Test classes that read from or write to the process-wide connection string cache in
    /// <see cref="Datadog.Trace.Util.DbCommandCache"/>. It disables itself once it has seen more
    /// distinct connection strings than its capacity, so a class that fills it up and a class that
    /// only adds a few entries must not run at the same time.
    /// </summary>
    [CollectionDefinition(nameof(DbCommandCacheTestCollection), DisableParallelization = true)]
    public class DbCommandCacheTestCollection
    {
    }
}
