using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Threading;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Samples.Yarp.DistributedTracing
{
    /// <summary>
    /// Based on CustomProxyConfigProvider from https://github.com/fzankl/yarp-sample/blob/main/src/ReverseProxy/CustomProxyConfigProvider.cs
    /// </summary>
    public class CodeProxyConfigProvider : IProxyConfigProvider
    {
        private CustomMemoryConfig _config;

        public CodeProxyConfigProvider()
        {
            _config = GenerateProxyConfig("https://example.com");
        }

        public IProxyConfig GetConfig() => _config;

        /// <summary>
        /// By calling this method from the source we can dynamically adjust the proxy configuration.
        /// Since our provider is registered in DI mechanism it can be injected via constructors anywhere.
        /// </summary>
        public void Update(string destinationUri)
        {
            var oldConfig = _config;
            _config = GenerateProxyConfig(destinationUri);
            oldConfig.SignalChange();
        }

        private CustomMemoryConfig GenerateProxyConfig(string destinationUri)
        {
            var routeConfig = new RouteConfig
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/proxy/{**catch-all}"
                }
            };

            routeConfig = routeConfig
                .WithTransformPathRemovePrefix(prefix: "/proxy");

            var routeConfigs = new[] { routeConfig };

            var clusterConfigs = new[]
            {
                new ClusterConfig
                {
                    ClusterId = "cluster1",
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { "destination1", new DestinationConfig { Address = destinationUri } },
                    }
                }
            };

            return new CustomMemoryConfig(routeConfigs, clusterConfigs);
        }

        private class CustomMemoryConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public CustomMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<RouteConfig> Routes { get; }

            public IReadOnlyList<ClusterConfig> Clusters { get; }

            public IChangeToken ChangeToken { get; }

            internal void SignalChange()
            {
                _cts.Cancel();
            }
        }
    }
}
