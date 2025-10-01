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
            result["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
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
            var libConfig = result["lib_config"];

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
            result["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void MergeConfigurations_NoMatchingConfigs_ReturnsNonEmpty()
        {
            // We have decided for now to not filter out non-matching configs. We will address it in a later PR.
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
            result?["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
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

            result["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void MergeConfigurations_ComplexScenario_CorrectPriorityMerging()
        {
            // Arrange - Following the priority order (bit-based calculation):
            // 6 (110): Service+env (highest priority)
            // 4 (100): Service only
            // 2 (010): Env only
            // 1 (001): Cluster-target only
            // 0 (000): Org level (lowest priority)

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
            var libConfig = result["lib_config"];

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
        [InlineData("test-service", "production", "test-cluster", 7)]   // Service+env+cluster (111 binary = 7)
        [InlineData("test-service", "production", null, 6)] // Service+env (110 binary = 6)
        [InlineData("test-service", "*", "test-cluster", 5)]            // Service+cluster (101 binary = 5)
        [InlineData("test-service", "*", null, 4)]          // Service only (100 binary = 4)
        [InlineData("*", "production", "test-cluster", 3)]              // Env+cluster (011 binary = 3)
        [InlineData("*", "production", null, 2)]            // Env only (010 binary = 2)
        [InlineData(null, null, "test-cluster", 1)]                     // Cluster only (001 binary = 1)
        [InlineData("*", "*", null, 0)]                     // Wildcard, Org level (000 binary = 0)
        [InlineData(null, null, null, 0)]                   // Org level (000 binary = 0)
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
                clusterTarget = new K8sTargetV2
                {
                    ClusterTargets = [new() { ClusterName = cluster }]
                };
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

            var clusterConfig = CreateRemoteConfig(
                "cluster",
                new
                {
                    lib_config = new { tracing_enabled = true },
                    k8s_target_v2 = new { cluster_targets = new[] { new { cluster_name = "prod-cluster" } } }
                });

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
            var libConfig = result["lib_config"];

            // Env config should override cluster for log_injection_enabled
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeTrue();

            // Env config should override cluster for tracing_enabled
            // But if env doesn't specify it, cluster should override org
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_ServiceEnvOverridesCluster_CorrectPriority()
        {
            // Arrange
            var clusterConfig = CreateRemoteConfig(
                "cluster",
                new
                {
                    lib_config = new { tracing_enabled = false },
                    k8s_target_v2 = new { cluster_targets = new[] { new { cluster_name = "prod-cluster" } } }
                });

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
            // Service+Env should override cluster
            result["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void MergeConfigurations_AllPriorityLevels_CorrectOrdering()
        {
            // Arrange - Test all 5 priority levels (org, cluster, env, service, service+env)
            var orgConfig = CreateRemoteConfig(
                "org",
                new { lib_config = new { tracing_enabled = true, value = "org" } });

            var clusterConfig = CreateRemoteConfig(
                "cluster",
                new
                {
                    lib_config = new { log_injection_enabled = true, value = "cluster" },
                    k8s_target_v2 = new { cluster_targets = new[] { new { cluster_name = "test" } } }
                });

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
            var libConfig = result["lib_config"];

            // Verify priority ordering:
            libConfig?["tracing_tags"]?.Value<string>().Should().Be("tags");            // Service+Env (6)
            libConfig?["tracing_header_tags"]?.Value<string>().Should().Be("headers");  // Service (4)
            libConfig?["tracing_sampling_rate"]?.Value<double>().Should().Be(0.3);      // Env (2)
            libConfig?["log_injection_enabled"]?.Value<bool>().Should().BeTrue();               // Cluster (1)
            libConfig?["tracing_enabled"]?.Value<bool>().Should().BeTrue();                     // Org (0)
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
                new ServiceTarget { Service = "test-service", Env = "production" }, // Service+env (priority 6)
                null);

            // Act
            var merged = lowPriorityConfig.MergeWith(highPriorityConfig);

            // Assert
            merged.ConfigId.Should().Be("high"); // Higher priority config's ID
            merged.Priority.Should().Be(6); // Higher priority (service+env = 110 binary = 6)
            merged.LibConfig.TracingEnabled.Should().BeTrue(); // From high priority
            merged.LibConfig.LogInjectionEnabled.Should().BeTrue(); // From low priority
            merged.LibConfig.TracingSamplingRate.Should().Be(0.8); // From high priority
        }

        [Fact]
        public void MergeConfigurations_ReturnsValidJToken()
        {
            // Arrange
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

            var configs = new List<RemoteConfiguration> { orgConfig, serviceConfig };

            // Act
            var result = ApmTracingConfigMerger.MergeConfigurations(configs, "test-service", "production");

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(JTokenType.Object);
            result["lib_config"].Should().NotBeNull();
            result["lib_config"]?["log_injection_enabled"]?.Value<bool>().Should().BeFalse();
            result["lib_config"]?["tracing_enabled"]?.Value<bool>().Should().BeTrue();
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
