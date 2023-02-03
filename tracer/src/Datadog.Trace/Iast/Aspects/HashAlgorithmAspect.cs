// <copyright file="HashAlgorithmAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects
{
    /// <summary> HashAlgorithm class aspects </summary>
    [AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal partial class HashAlgorithmAspect
    {
        /// <summary>
        /// ComputeHash not static
        /// </summary>
        /// <param name="target">main HashAlgorithm instance (not static)</param>
        [AspectMethodInsertBefore("System.Security.Cryptography.HashAlgorithm::ComputeHash()")]
        internal static void ComputeHash(HashAlgorithm target)
        {
        }
    }
}
