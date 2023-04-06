// <copyright file="IQueryResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.IQueryResult interface for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/35301472065248ce4e2f34894041f39124e3c7b8/src/HotChocolate/Core/src/Abstractions/Execution/IQueryResult.cs
    /// </summary>
    internal interface IQueryResult
    {
        /// <summary>
        /// Gets the executing operation errors
        /// </summary>
        IEnumerable Errors { get; }
    }
}
