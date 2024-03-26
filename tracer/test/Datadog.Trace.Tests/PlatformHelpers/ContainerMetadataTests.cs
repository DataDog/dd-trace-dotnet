// <copyright file="ContainerMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
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

        public const string EksNodegroup = @"
11:blkio:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
10:cpuset:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
9:perf_event:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
8:memory:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
7:pids:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
6:cpu,cpuacct:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
5:net_cls,net_prio:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
4:devices:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
3:freezer:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
2:hugetlb:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
1:name=systemd:/kubepods.slice/kubepods-pod9508fe66_7675_4003_b7c9_d83e9f8f85e5.slice/cri-containerd-26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4.scope
";

        public const string PcfContainer1 =
            """
            12:memory:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            11:rdma:/
            10:freezer:/garden/6f265890-5165-7fab-6b52-18d1
            9:hugetlb:/garden/6f265890-5165-7fab-6b52-18d1
            8:pids:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            7:perf_event:/garden/6f265890-5165-7fab-6b52-18d1
            6:cpu,cpuacct:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            5:net_cls,net_prio:/garden/6f265890-5165-7fab-6b52-18d1
            4:cpuset:/garden/6f265890-5165-7fab-6b52-18d1
            3:blkio:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            2:devices:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            1:name=systemd:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1
            """;

        public const string PcfContainer2 = "1:name=systemd:/system.slice/garden.service/garden/6f265890-5165-7fab-6b52-18d1";

        public const string InodeCgroupV2 =
            """
            0::/system.slice/docker-abcdef0123456789abcdef0123456789.scope
            """;

        public const string InodeCgroupV2EmptyNodePath =
            """
            0::/
            """;

        public const string InodeCgroupV1 =
            """
            3:memory:/system.slice/docker-abcdef0123456789abcdef0123456789.scope
            2:net_cls,net_prio:c
            1:name=systemd:b
            0::a
            """;

        public const string InodeCgroupV1UnrecognizedController =
            """
            3:cpu:/system.slice/docker-abcdef0123456789abcdef0123456789.scope
            2:net_cls,net_prio:c
            1:name=systemd:b
            0::a
            """;

        public const string InodeCgroupV1NoEntries = "nothing";

        public static IEnumerable<object[]> GetContainerIds()
        {
            yield return new object[] { Docker, "3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860" };
            yield return new object[] { Kubernetes, "3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1" };
            yield return new object[] { Ecs, "38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce" };
            yield return new object[] { Fargate1Dot3, "432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da" };
            yield return new object[] { Fargate1Dot4, "34dc0b5e626f2c5c4c5170e34b10e765-1234567890" };
            yield return new object[] { EksNodegroup, "26cfbe35e08b24f053011af4ada23d8fcbf81f27f8331a94f56de5b677c903e4" };
            yield return new object[] { PcfContainer1, "6f265890-5165-7fab-6b52-18d1" };
            yield return new object[] { PcfContainer2, "6f265890-5165-7fab-6b52-18d1" };
        }

        public static IEnumerable<object[]> GetInodes()
        {
            yield return new object[] { InodeCgroupV2, "system.slice/docker-abcdef0123456789abcdef0123456789.scope", true };
            yield return new object[] { InodeCgroupV2EmptyNodePath, string.Empty, true };
            yield return new object[] { InodeCgroupV1, "memory/system.slice/docker-abcdef0123456789abcdef0123456789.scope", true };
            yield return new object[] { InodeCgroupV1, "dummy.scope", false };
            yield return new object[] { InodeCgroupV1UnrecognizedController, "cpu/system.slice/docker-abcdef0123456789abcdef0123456789.scope", false };
            yield return new object[] { InodeCgroupV1NoEntries, "dummy.scope", false };
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
        [MemberData(nameof(GetContainerIds))]
        public void Parse_ContainerId_From_Cgroup_File(string file, string expected)
        {
            // arrange
            var lines = SplitLines(file);

            // act
            string actual = ContainerMetadata.ParseContainerIdFromCgroupLines(lines);

            // assert
            Assert.Equal(expected, actual);
        }

        [SkippableTheory]
        [MemberData(nameof(GetInodes))]
        public void Parse_Inode_From_Cgroup_File(string file, string relativePathToCreate, bool isSuccess)
        {
            if (!EnvironmentTools.IsLinux())
            {
                throw new SkipException("Obtaining the inode is only supported on Linux");
            }

            // arrange
            var lines = SplitLines(file);

            // Set up directory on disk for testing
            string sysFsCgroupPath = Path.Combine(Path.GetTempPath(), $"temp-sysfscgroup-{Guid.NewGuid():n}");
            string controllerCgroupPath = Path.Combine(sysFsCgroupPath, relativePathToCreate);
            Directory.CreateDirectory(controllerCgroupPath);
            string expected = isSuccess && ContainerMetadata.TryGetInode(controllerCgroupPath, out long inode) ? inode.ToString() : null;

            // act
            string actual = ContainerMetadata.ExtractInodeFromCgroupLines(sysFsCgroupPath, lines);

            // assert
            try
            {
                Assert.Equal(expected, actual);
            }
            finally
            {
                Directory.Delete(sysFsCgroupPath, recursive: true);
            }
        }
    }
}
