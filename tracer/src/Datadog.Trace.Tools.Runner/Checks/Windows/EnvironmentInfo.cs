// <copyright file="EnvironmentInfo.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Runner.Checks.Windows
{
    internal static class EnvironmentInfo
    {
        static EnvironmentInfo()
        {
            var os = Environment.OSVersion;
            if (os.Platform == PlatformID.Win32NT && os.Version < new Version(6, 0))
            {
                // Windows Server 2003 and Windows XP: The maximum size of the environment block for the process is 32,767 characters.
                MaxSize = 32767;
            }
            else
            {
                // Starting with Windows Vista and Windows Server 2008, there is no technical limitation on the size of the environment block.
                MaxSize = -1;
            }
        }

        /// <summary>
        /// Gets the maximum environment block size or -1 if there is no limit.
        /// </summary>
        public static int MaxSize { get; }
    }
}
