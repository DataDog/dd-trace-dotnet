// <copyright file="ProcessMemoryAdapter.cs" company="Datadog">
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

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Shared.Windows
{
    internal sealed class ProcessMemoryAdapter : IProcessMemoryAdapter
    {
        private readonly IntPtr _hProcess;

        public ProcessMemoryAdapter(IntPtr hProcess)
        {
            _hProcess = hProcess;
        }

        public int PageSize => SystemInfo.PageSize;

        public unsafe int ReadMemory(UniPtr address, byte[] buffer, int offset, int count, bool throwOnError)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            var actualCount = IntPtr.Zero;
            bool result;

            fixed (byte* p = buffer)
            {
                result = NativeMethods.ReadProcessMemory(
                    _hProcess,
                    address,
                    p + offset,
                    new IntPtr(count),
                    ref actualCount);
            }

            if (!result)
            {
                if (throwOnError)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0)
                    {
                        throw new Win32Exception(error);
                    }
                }

                return -1;
            }

            return actualCount.ToInt32();
        }
    }
}
