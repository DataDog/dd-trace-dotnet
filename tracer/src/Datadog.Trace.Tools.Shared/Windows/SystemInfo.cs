// <copyright file="SystemInfo.cs" company="Datadog">
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
    internal static class SystemInfo
    {
        static SystemInfo()
        {
            NativeMethods.GetSystemInfo(out var systemInfo);
            PageSize = checked((int)systemInfo.dwPageSize);
        }

        public static int PageSize { get; }

        public static class Native
        {
            static Native()
            {
                NativeMethods.GetNativeSystemInfo(out var systemInfo);
                PageSize = checked((int)systemInfo.dwPageSize);
            }

            public static int PageSize { get; }
        }
    }
}
