// <copyright file="IError.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.IError interface for ducktyping
    /// </summary>
    internal interface IError
    {
        /// <summary>
        /// Gets a code for the error
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets a list of locations in the document where the error applies
        /// </summary>
        IEnumerable Locations { get; }

        /// <summary>
        /// Gets a message for the error
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the path in the document where the error applies.
        /// Returns the HotChocolate.Path object which has a ToList() method.
        /// </summary>
        object Path { get; }

        /// <summary>
        /// Gets the StackTrace of the error.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Gets additional Extensions information about error.
        /// </summary>
        IReadOnlyDictionary<string, object> Extensions { get; }
    }
}
