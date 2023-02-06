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
        /// <param name="stream">ComputeHash stream</param>
        [AspectMethodInsertBefore("System.Security.Cryptography.HashAlgorithm::ComputeHash(System.IO.Stream)")]
        internal static void ComputeHash(HashAlgorithm target, System.IO.Stream stream)
        {
            Console.WriteLine("INSIDE ASPECT");
        }

        /// <summary>
        /// ComputeHash not static
        /// </summary>
        /// <param name="target">main HashAlgorithm instance (not static)</param>
        /// <param name="buffer">ComputeHash buffer</param>
        /// <param name="offset">ComputeHash offset</param>
        /// <param name="count">ComputeHash count</param>
        [AspectMethodInsertBefore("System.Security.Cryptography.HashAlgorithm::ComputeHash(System.Byte[],System.Int32,System.Int32)")]
        internal static void ComputeHash(HashAlgorithm target, byte[] buffer, int offset, int count)
        {
            Console.WriteLine("INSIDE ASPECT");
        }

        /// <summary>
        /// ComputeHash not static
        /// </summary>
        /// <param name="target">main HashAlgorithm instance (not static)</param>
        /// <param name="buffer">ComputeHash buffer</param>
        [AspectMethodInsertBefore("System.Security.Cryptography.HashAlgorithm::ComputeHash(System.Byte[])")]
        internal static void ComputeHash(HashAlgorithm target, byte[] buffer)
        {
            Console.WriteLine("INSIDE ASPECT");
        }
    }
}
