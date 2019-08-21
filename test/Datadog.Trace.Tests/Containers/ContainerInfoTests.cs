using System.Collections.Generic;
using Datadog.Trace.Containers;
using Xunit;

namespace Datadog.Trace.Tests.Containers
{
    public class ContainerInfoTests
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

        public const string Fargate = @"
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

        public static IEnumerable<object[]> GetCgroupFiles()
        {
            yield return new object[] { Docker, "3726184226f5d3147c25fdeab5b60097e378e8a720503a5e19ecfdf29f869860" };
            yield return new object[] { Kubernetes, "3e74d3fd9db4c9dd921ae05c2502fb984d0cde1b36e581b13f79c639da4518a1" };
            yield return new object[] { Ecs, "38fac3e99302b3622be089dd41e7ccf38aff368a86cc339972075136ee2710ce" };
            yield return new object[] { Fargate, "432624d2150b349fe35ba397284dea788c2bf66b885d14dfc1569b01890ca7da" };
        }

        [Theory]
        [MemberData(nameof(GetCgroupFiles))]
        public void ParseFile(string file, string expected)
        {
            string actual = ContainerInfo.ParseCgroupText(file);
            Assert.Equal(expected, actual);
        }
    }
}
