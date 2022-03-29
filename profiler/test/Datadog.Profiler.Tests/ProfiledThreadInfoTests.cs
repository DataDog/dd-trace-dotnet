// <copyright file="ProfiledThreadInfoTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Datadog.Profiler.Tests
{
    public class ProfiledThreadInfoTests
    {
        [Fact]
        public void EnsureProfiledThreadInfoCreationIsCorrect()
        {
            uint expectedTheadInfoId = 42;
            int expectedSessionId = 21;
            var info = new ProfiledThreadInfo(expectedTheadInfoId, expectedSessionId);
            Assert.Equal(expectedTheadInfoId, info.ProfilerThreadInfoId);
            Assert.Equal(expectedSessionId, info.ProviderSessionId);
            Assert.Equal(0UL, info.ClrThreadId);
            Assert.Equal(0U, info.OsThreadId);
            Assert.Equal((IntPtr)0, info.OsThreadHandle);
            Assert.Equal(string.Empty, info.ThreadName);
        }

        [Fact]
        public void EnsureProfiledThreadInfoUpdateIsCorrect()
        {
            var info = new ProfiledThreadInfo(42, 21);

            int expectedProviderSessionId = 1;
            ulong expectedClrThreadId = 2;
            uint expectedOsThreadId = 3;
            IntPtr expectedOsThreadHandle = (IntPtr)73;
            var expectedThreadName = "ThreadName";

            info.UpdateProperties(expectedProviderSessionId, expectedClrThreadId, expectedOsThreadId, expectedOsThreadHandle, expectedThreadName);

            Assert.Equal(1, info.ProviderSessionId);
            Assert.Equal(expectedClrThreadId, info.ClrThreadId);
            Assert.Equal(expectedOsThreadId, info.OsThreadId);
            Assert.Equal(expectedOsThreadHandle, info.OsThreadHandle);
            Assert.Equal(expectedThreadName, info.ThreadName);
        }
    }
}
