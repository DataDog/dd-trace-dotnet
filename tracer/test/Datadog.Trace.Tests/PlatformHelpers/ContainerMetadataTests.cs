// <copyright file="ContainerMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.PlatformHelpers;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class ContainerMetadataTests
    {
        public const string Docker = @"
13:name=systemd:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
12:pids:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
11:hugetlb:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
10:net_prio:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
9:perf_event:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
8:net_cls:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
7:freezer:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
6:devices:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
5:memory:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
4:blkio:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
3:cpuacct:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
2:cpu:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860
1:cpuset:/docker/3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860";

        public const string Kubernetes = @"
11:perf_event:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
10:pids:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
9:memory:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
8:cpu,cpuacct:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
7:blkio:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
6:cpuset:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
5:devices:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
4:freezer:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
3:net_cls,net_prio:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
2:hugetlb:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
1:name=systemd:/kubepods/besteffort/pod3d274242-8ee0-11e9-a8a6-1e68d864ef1a/3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1
1:name=systemd:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod2d3da189_6407_48e3_9ab6_78188d75e609.slice/docker-3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1.scope
";

        public const string Ecs = @"
9:perf_event:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
8:memory:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
7:hugetlb:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
6:freezer:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
5:devices:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
4:cpuset:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
3:cpuacct:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
2:cpu:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
1:blkio:/ecs/haissam-ecs-classic/5a0d5ceddf6c44c1928d367a815d890f/38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce
";

        public const string Fargate1Dot3 = @"
11:hugetlb:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
10:pids:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
9:cpuset:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
8:net_cls,net_prio:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
7:cpu,cpuacct:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
6:perf_event:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
5:freezer:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
4:devices:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
3:blkio:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
2:memory:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
1:name=systemd:/ecs/55091c13-b8cf-4801-b527-f4601742204d/432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da
";

        public const string Fargate1Dot4 = @"
11:hugetlb:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
10:pids:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
9:cpuset:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
8:net_cls,net_prio:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
7:cpu,cpuacct:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
6:perf_event:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
5:freezer:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
4:devices:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
3:blkio:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
2:memory:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
1:name=systemd:/ecs/34dc0b5e626f2c5c4c5170e34b10e765-1234567890
";

        public static IEnumerable<object[]> GetCgroupFiles()
        {
            yield return new object[] { Docker, "3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860" };
            yield return new object[] { Kubernetes, "3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1" };
            yield return new object[] { Ecs, "38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce" };
            yield return new object[] { Fargate1Dot3, "432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da" };
            yield return new object[] { Fargate1Dot4, "34dc0b5e626f2c5c4c5170e34b10e765-1234567890" };
        }

        /// <summary>
        /// Splits multi-line string into individual strings, one string per line.
        /// </summary>
        /// <param name="contents">The multi-line string.</param>
        /// <returns>An enumerable that returns each line from <paramref name="contents"/> when iterated.</returns>
        public static IEnumerable<string> SplitLines(string contents)
        {
            if (contents == null)
            {
                yield break;
            }

            using (var reader = new StringReader(contents))
            {
                while (true)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        yield break;
                    }

                    yield return line;
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetCgroupFiles))]
        public void Parse_ContainerId_From_Cgroup_File(string file, string expected)
        {
            // arrange
            var lines = SplitLines(file);

            // act
            string actual = ContainerMetadata.ParseCgroupLines(lines);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
