// <copyright file="ApmTracingConfigMergerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ApmTracingConfigMergerTests
    {
        [Fact]
        public void MergeConfigurations_SingleConfig_ReturnsOriginal()
        {
            // Arrange
            var config = CreateRemoteConfig("config1", new { lib_config = new { tracing_enabled = true } });
            var configs = new List<RemoteConfiguration> { config };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "test-env");

            // Assert
            var resultJson = JObject.Parse(result);
            resultJson["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_PriorityOrdering_ServiceAndEnvWins()
        {
            // Arrange
            var orgConfig = CreateRemoteConfig(
                "org",
                new
                {
                    lib_config = new { tracing_enabled = false, log_injection_enabled = true }
                });

            var envConfig = CreateRemoteConfig(
                "env",
                new
                {
                    service_target = new { env = "production" },
                    lib_config = new { tracing_enabled = true }
                });

            var serviceEnvConfig = CreateRemoteConfig(
                "service-env",
                new
                {
                    service_target = new { service = "test-service", env = "production" },
                    lib_config = new { log_injection_enabled = false }
                });

            var configs = new List<RemoteConfiguration> { orgConfig, envConfig, serviceEnvConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);
            var libConfig = resultJson["lib_config"];

            // Service+Env config should override others for log_injection_enabled
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeFalse();

            // Env config should override org for tracing_enabled
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_WildcardMatching_MatchesAllServices()
        {
            // Arrange
            var wildcardConfig = CreateRemoteConfig(
                "wildcard",
                new
                {
                    service_target = new { service = "*", env = "production" },
                    lib_config = new { tracing_enabled = false }
                });

            var configs = new List<RemoteConfiguration> { wildcardConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "any-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);
            resultJson["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void MergeConfigurations_NoMatchingConfigs_ReturnsEmpty()
        {
            // Arrange
            var config = CreateRemoteConfig(
                "config1",
                new
                {
                    service_target = new { service = "other-service", env = "staging" },
                    lib_config = new { tracing_enabled = false }
                });

            var configs = new List<RemoteConfiguration> { config };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            result.Should().Be("{\"lib_config\":{}}");
        }

        [Fact]
        public void MergeConfigurations_EmptyConfigs_ReturnsOriginal()
        {
            // Arrange
            var config = CreateRemoteConfig(
                "config1",
                new
                {
                    lib_config = new { tracing_enabled = false }
                });

            var configs = new List<RemoteConfiguration> { config };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            var resultJson = JObject.Parse(result);
            resultJson["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void MergeConfigurations_ComplexScenario_CorrectPriorityMerging()
        {
            // Arrange - Following the priority order:
            // 1. Service+env (highest priority)
            // 2. Service
            // 3. Env
            // 4. Cluster-target (not implemented)
            // 5. Org level (lowest priority)

            var orgConfig = CreateRemoteConfig(
                "org",
                new
                {
                    lib_config = new
                    {
                        tracing_enabled = true,
                        log_injection_enabled = true,
                        tracing_sampling_rate = 0.1,
                        tracing_tags = "[\"org:global\"]"
                    }
                });

            var envConfig = CreateRemoteConfig(
                "env",
                new
                {
                    service_target = new { env = "production" },
                    lib_config = new
                    {
                        tracing_enabled = false,
                        tracing_sampling_rate = 0.5,
                        tracing_header_tags = "[{\"header\":\"User-Agent\",\"tag_name\":\"http.user_agent\"}]"
                    }
                });

            var serviceConfig = CreateRemoteConfig(
                "service",
                new
                {
                    service_target = new { service = "test-service" },
                    lib_config = new
                    {
                        log_injection_enabled = false,
                        tracing_sampling_rules = "[{\"sample_rate\":0.9}]"
                    }
                });

            var serviceEnvConfig = CreateRemoteConfig(
                "service-env",
                new
                {
                    service_target = new { service = "test-service", env = "production" },
                    lib_config = new
                    {
                        tracing_sampling_rate = 0.8
                    }
                });

            var configs = new List<RemoteConfiguration> { orgConfig, envConfig, serviceConfig, serviceEnvConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);
            var libConfig = resultJson["lib_config"];

            // Service+Env (highest priority) - should set tracing_sampling_rate
            libConfig?["tracing_sampling_rate"]?.Value<double>().Should().Be(0.8);

            // Service (second highest) - should set log_injection_enabled and tracing_sampling_rules
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeFalse();
            libConfig?["tracing_sampling_rules"]?.Value<string>().Should().Be("[{\"sample_rate\":0.9}]");

            // Env (third highest) - should set tracing_enabled and tracing_header_tags
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
            libConfig?["tracing_header_tags"]?.Value<string>().Should().Be("[{\"header\":\"User-Agent\",\"tag_name\":\"http.user_agent\"}]");

            // Org (lowest priority) - should set tracing_tags (only field not overridden)
            libConfig?["tracing_tags"]?.Value<string>().Should().Be("[\"org:global\"]");
        }

        [Theory]
        [InlineData("*", "production", true)]
        [InlineData("test-service", "*", true)]
        [InlineData("*", "*", true)]
        [InlineData("other-service", "production", false)]
        [InlineData("test-service", "staging", false)]
        [InlineData("other-service", "staging", false)]
        public void ApmTracingConfig_Matches_CorrectBehavior(string configService, string configEnv, bool shouldMatch)
        {
            // Arrange
            var libConfig = new LibConfig { TracingEnabled = true };
            var serviceTarget = new ServiceTarget { Service = configService, Env = configEnv };
            var config = new ApmTracingConfig("test", libConfig, serviceTarget, null);

            // Act
            var matches = config.Matches("test-service", "production");

            // Assert
            matches.Should().Be(shouldMatch);
        }

        [Theory]
        [InlineData("test-service", "production", null, 5)] // Service+env
        [InlineData("test-service", "*", null, 4)]          // Service only
        [InlineData("*", "production", null, 3)]            // Env only
        [InlineData(null, null, "", 2)]                     // Cluster only
        [InlineData("*", "*", null, 1)]                     // Wildcard, Org level
        [InlineData(null, null, null, 1)]                   // Org level
        public void ApmTracingConfig_Priority_CorrectValues(string? service, string? env, string? cluster, int expectedPriority)
        {
            // Arrange
            var libConfig = new LibConfig { TracingEnabled = true };

            ServiceTarget? serviceTarget = null;
            if (service != null || env != null)
            {
                serviceTarget = new ServiceTarget { Service = service, Env = env };
            }

            K8sTargetV2? clusterTarget = null;
            if (cluster != null)
            {
                clusterTarget = new K8sTargetV2();
            }

            var config = new ApmTracingConfig("test", libConfig, serviceTarget, clusterTarget);

            // Act & Assert
            config.Priority.Should().Be(expectedPriority);
        }

        [Fact]
        public void MergeConfigurations_ClusterTargetPriority_CorrectOrdering()
        {
            // Arrange
            var orgConfig = CreateRemoteConfig(
                "org",
                new { lib_config = new { tracing_enabled = false } });

            var clusterConfig = CreateRemoteConfigWithCluster(
                "cluster",
                new { tracing_enabled = true },
                new { cluster_targets = new[] { new { cluster_name = "prod-cluster" } } });

            var envConfig = CreateRemoteConfig(
                "env",
                new
                {
                    service_target = new { env = "production" },
                    lib_config = new { log_injection_enabled = true }
                });

            var configs = new List<RemoteConfiguration> { orgConfig, clusterConfig, envConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);
            var libConfig = resultJson["lib_config"];

            // Env config (priority 3) should override cluster (priority 2) for log_injection_enabled
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeTrue();

            // Env config (priority 3) should override cluster (priority 2) for tracing_enabled
            // But if env doesn't specify it, cluster should override org
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_ServiceEnvOverridesCluster_CorrectPriority()
        {
            // Arrange
            var clusterConfig = CreateRemoteConfigWithCluster(
                "cluster",
                new { tracing_enabled = false },
                new { cluster_targets = new[] { new { cluster_name = "prod-cluster" } } });

            var serviceEnvConfig = CreateRemoteConfig(
                "service-env",
                new
                {
                    service_target = new { service = "test-service", env = "production" },
                    lib_config = new { tracing_enabled = true }
                });

            var configs = new List<RemoteConfiguration> { clusterConfig, serviceEnvConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);

            // Service+Env (priority 5) should override cluster (priority 2)
            resultJson["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_AllPriorityLevels_CorrectOrdering()
        {
            // Arrange - Test all 5 priority levels
            var orgConfig = CreateRemoteConfig(
                "org",
                new { lib_config = new { tracing_enabled = true, value = "org" } });

            var clusterConfig = CreateRemoteConfigWithCluster(
                "cluster",
                new { log_injection_enabled = true, value = "cluster" },
                new { cluster_targets = new[] { new { cluster_name = "test" } } });

            var envConfig = CreateRemoteConfig(
                "env",
                new
                {
                    service_target = new { env = "production" },
                    lib_config = new { tracing_sampling_rate = 0.3, value = "env" }
                });

            var serviceConfig = CreateRemoteConfig(
                "service",
                new
                {
                    service_target = new { service = "test-service" },
                    lib_config = new { tracing_header_tags = "headers", value = "service" }
                });

            var serviceEnvConfig = CreateRemoteConfig(
                "service-env",
                new
                {
                    service_target = new { service = "test-service", env = "production" },
                    lib_config = new { tracing_tags = "tags", value = "service-env" }
                });

            var configs = new List<RemoteConfiguration>
            {
                orgConfig, clusterConfig, envConfig, serviceConfig, serviceEnvConfig
            };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            var resultJson = JObject.Parse(result);
            var libConfig = resultJson["lib_config"];

            // Verify priority ordering:
            libConfig?["tracing_tags"]?.Value<string>().Should().Be("tags");            // Service+Env (5)
            libConfig?["tracing_header_tags"]?.Value<string>().Should().Be("headers");  // Service (4)
            libConfig?["tracing_sampling_rate"]?.Value<double>().Should().Be(0.3);      // Env (3)
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeTrue();               // Cluster (2)
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeTrue();                     // Org (1)
        }

        [Fact]
        public void ApmTracingConfig_MergeWith_HigherPriorityWins()
        {
            // Arrange
            var lowPriorityConfig = new ApmTracingConfig(
                "low",
                new LibConfig
                {
                    TracingEnabled = false,
                    LogInjectionEnabled = true,
                    TracingSamplingRate = 0.1
                },
                null, // Org level (priority 0)
                null);

            var highPriorityConfig = new ApmTracingConfig(
                "high",
                new LibConfig
                {
                    TracingEnabled = true,
                    TracingSamplingRate = 0.8

                    // LogInjectionEnabled is null, should use value from low priority
                },
                new ServiceTarget { Service = "test-service", Env = "production" }, // Service+env (priority 5)
                null);

            // Act
            var merged = lowPriorityConfig.MergeWith(highPriorityConfig);

            // Assert
            merged.ConfigId.Should().Be("high"); // Higher priority config's ID
            merged.Priority.Should().Be(5); // Higher priority
            merged.LibConfig.TracingEnabled.Should().BeTrue(); // From high priority
            merged.LibConfig.LogInjectionEnabled.Should().BeTrue(); // From low priority
            merged.LibConfig.TracingSamplingRate.Should().Be(0.8); // From high priority
        }

        private static RemoteConfiguration CreateRemoteConfigWithCluster(string id, object libConfig, object k8sTarget)
        {
            var content = new
            {
                lib_config = libConfig,
                k8s_target_v2 = k8sTarget
            };

            var json = JsonConvert.SerializeObject(content);
            var bytes = Encoding.UTF8.GetBytes(json);
            var path = RemoteConfigurationPath.FromPath($"datadog/123/APM_TRACING/{id}/config");

            return new RemoteConfiguration(
                path: path,
                contents: bytes,
                length: bytes.Length,
                hashes: new Dictionary<string, string> { { "sha256", "dummy-hash" } },
                version: 1);
        }

        private static RemoteConfiguration CreateRemoteConfig(string id, object content)
        {
            var json = JsonConvert.SerializeObject(content);
            var bytes = Encoding.UTF8.GetBytes(json);
            var path = RemoteConfigurationPath.FromPath($"datadog/123/APM_TRACING/{id}/config");

            return new RemoteConfiguration(
                path: path,
                contents: bytes,
                length: bytes.Length,
                hashes: new Dictionary<string, string> { { "sha256", "dummy-hash" } },
                version: 1);
        }
    }
}
