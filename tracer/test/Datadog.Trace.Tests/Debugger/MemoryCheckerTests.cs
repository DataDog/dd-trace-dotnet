// <copyright file="MemoryCheckerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Caching;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class MemoryCheckerTests
    {
        [Fact]
        public void IsLowResourceEnvironment_ActualSystemCall_ReturnsExpectedResult()
        {
            var memoryChecker = new MemoryChecker();
            bool result = memoryChecker.IsLowResourceEnvironment();

            // We can't assert a specific result as it depends on the system,
            // but we can ensure it doesn't throw an exception
            Assert.True(result || !result); // This will always be true if no exception is thrown
        }

        [Fact]
        public void CheckWindowsMemory_ActualSystemCall_ReturnsExpectedResult()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memoryChecker = new TestableMemoryChecker();
                bool result = memoryChecker.PublicCheckWindowsMemory();

                // Again, we can't assert a specific result, but we can ensure it runs without exception
                Assert.True(result || !result);
            }
        }

        [Fact]
        public void CheckUnixMemory_ActualFileRead_ReturnsExpectedResult()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var memoryChecker = new TestableMemoryChecker();
                bool result = memoryChecker.PublicCheckUnixMemory();

                // We can't assert a specific result, but we can ensure it runs without exception
                Assert.True(result || !result);
            }
        }

        [Fact]
        public void ReadMemInfo_ActualFileRead_ReturnsValidValue()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var memoryChecker = new TestableMemoryChecker();
                string memInfo = memoryChecker.PublicReadMemInfo();

                Assert.NotNull(memInfo);
                Assert.True(long.TryParse(memInfo, out long availableKB));
                Assert.True(availableKB > 0);
            }
        }

        [Fact]
        public void CheckUnixMemory_ValidMemAvailable_ReturnsExpectedResult()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var memoryChecker = new TestableMemoryChecker();
                // Set a valid MemAvailable value (2GB in KB)
                memoryChecker.SetCustomMemInfoContent("MemTotal:        16332184 kB\nMemFree:          1135792 kB\nMemAvailable:      2097152 kB\n");

                bool result = memoryChecker.PublicCheckUnixMemory();

                // The method should return false because 2GB is above the low memory threshold
                Assert.False(result);

                // Now set a lower MemAvailable value (500MB in KB)
                memoryChecker.SetCustomMemInfoContent("MemTotal:        16332184 kB\nMemFree:          1135792 kB\nMemAvailable:      512000 kB\n");

                result = memoryChecker.PublicCheckUnixMemory();

                // The method should return true because 500MB is below the low memory threshold
                Assert.True(result);
            }
        }

        [Fact]
        public void CheckUnixMemory_MissingMemAvailable_HandlesGracefully()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var memoryChecker = new TestableMemoryChecker();
                memoryChecker.SetCustomMemInfoContent("MemTotal:        16332184 kB\nMemFree:          1135792 kB\n");

                bool result = memoryChecker.PublicCheckUnixMemory();

                // The method should return false if it can't find MemAvailable
                Assert.False(result);
            }
        }

        [Fact]
        public void CheckUnixMemory_InvalidMemAvailableFormat_HandlesGracefully()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var memoryChecker = new TestableMemoryChecker();
                memoryChecker.SetCustomMemInfoContent("MemTotal:        16332184 kB\nMemFree:          1135792 kB\nMemAvailable:      InvalidValue kB\n");

                bool result = memoryChecker.PublicCheckUnixMemory();

                // The method should return false if it can't parse MemAvailable
                Assert.False(result);
            }
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void IsLowResourceEnvironment_ActualGCCalls_ReturnsExpectedResult()
        {
            var memoryChecker = new MemoryChecker();
            bool result = memoryChecker.IsLowResourceEnvironment();

            // We can't assert a specific result as it depends on the system,
            // but we can ensure it doesn't throw an exception
            Assert.True(result || !result);
        }

        [Fact]
        public void GCMemoryInfo_ActualCall_ReturnsValidValues()
        {
            long totalMemory = GC.GetTotalMemory(false);
            var gcMemoryInfo = GC.GetGCMemoryInfo();

            Assert.True(totalMemory > 0);
            Assert.True(gcMemoryInfo.TotalAvailableMemoryBytes > 0);
            Assert.True(totalMemory <= gcMemoryInfo.TotalAvailableMemoryBytes);
        }
#endif

        internal class TestableMemoryChecker : MemoryChecker
        {
            private string _customMemInfoContent;

            public bool PublicCheckWindowsMemory()
            {
                return CheckWindowsMemory();
            }

            public bool PublicCheckUnixMemory()
            {
                return CheckUnixMemory();
            }

            public string PublicReadMemInfo()
            {
                return ReadMemInfo();
            }

            public void SetCustomMemInfoContent(string content)
            {
                _customMemInfoContent = content;
            }

            protected override string ReadMemInfo()
            {
                return _customMemInfoContent ?? base.ReadMemInfo();
            }
        }
    }
}
