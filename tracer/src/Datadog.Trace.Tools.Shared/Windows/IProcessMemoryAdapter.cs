// <copyright file="IProcessMemoryAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Original code from https://github.com/gapotchenko/Gapotchenko.FX/tree/master/Source/Gapotchenko.FX.Diagnostics.Process
// MIT License
//
// Copyright © 2019 Gapotchenko and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

namespace Datadog.Trace.Tools.Shared.Windows
{
    /// <summary>
    /// Provides low-level access to the process memory.
    /// </summary>
    internal interface IProcessMemoryAdapter
    {
        /// <summary>
        /// Gets the page size measured in bytes according to the granularity of memory access control.
        /// </summary>
        int PageSize { get; }

        /// <summary>
        /// Reads the process memory.
        /// </summary>
        /// <param name="address">The address to start reading at.</param>
        /// <param name="buffer">The buffer to read to.</param>
        /// <param name="offset">The buffer offset to start reading to.</param>
        /// <param name="count">The count of bytes to read.</param>
        /// <param name="throwOnError">
        /// <para>
        /// Indicates whether to throw an exception on error.
        /// </para>
        /// <para>
        /// The support of this flag is optional; an adapter may just prefer to return -1 even when the flag is <c>true</c>.
        /// </para>
        /// </param>
        /// <returns>The count of read bytes or -1 on error.</returns>
        int ReadMemory(UniPtr address, byte[] buffer, int offset, int count, bool throwOnError);
    }
}
