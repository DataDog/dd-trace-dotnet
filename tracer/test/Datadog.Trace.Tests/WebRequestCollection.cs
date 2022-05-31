// <copyright file="WebRequestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests
{
    /// <summary>
    /// If you're using WebRequest (directly or indirectly), use must use this attribute
    /// As we have tests that modify the static prefixes.
    /// Or, you know, stop using WebRequest
    /// </summary>
    [CollectionDefinition(nameof(TracerInstanceTestCollection), DisableParallelization = true)]
    public class WebRequestCollection
    {
    }
}
