// <copyright file="OperationTypeProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// A proxy enum for GraphQL.Language.AST.OperationType.
    /// The enum values must match those of GraphQL.Language.AST.OperationType for spans
    /// to be decorated with the correct operation. Since the original type is public,
    /// we not expect changes between minor versions of the HotChocolate GraphQL library.
    /// </summary>
    internal enum OperationTypeProxy
    {
        /// <summary>
        /// A query operation.
        /// </summary>
        Query,

        /// <summary>
        /// A mutation operation.
        /// </summary>
        Mutation,

        /// <summary>
        /// A subscription operation.
        /// </summary>
        Subscription
    }
}
