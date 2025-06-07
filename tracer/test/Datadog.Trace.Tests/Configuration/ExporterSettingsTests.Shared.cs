// <copyright file="ExporterSettingsTests.Shared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

/// <summary>
/// Testing for the parts of exporter settings that are shared between Datadog.Trace and the dd-dotnet tool
/// </summary>
public partial class ExporterSettingsTests
{
    [Fact]
    public void DefaultValues()
    {
        var settings = new ExporterSettings();
        CheckDefaultValues(settings);
    }

    [Fact]
    public void AgentUri()
    {
        var param = "http://someUrl";
        var uri = new Uri(param);
        var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);

        AssertHttpIsConfigured(settingsFromSource, uri);
        // The Uri is used to connect to dogstatsd as well, by getting the host from the uri
        AssertMetricsUdpIsConfigured(settingsFromSource, "someurl");
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData(@"C:\temp\someval")]
    // [InlineData(@"\\temp\someval")] // network path doesn't work with uris, so we don't support it :/
    public void UnixAgentUriOnWindows(string path)
    {
        var uri = new Uri($"unix://{path}");

        var settingsFromSource = Setup(FileExistsMock(path.Replace("\\", "/")), $"DD_TRACE_AGENT_URL:unix://{path}");
        AssertUdsIsConfigured(settingsFromSource, path.Replace("\\", "/"));
        settingsFromSource.AgentUri.Should().Be(uri);
        settingsFromSource.ValidationWarnings.Should().BeEmpty();
        // Without additional settings, metrics defaults to UDP even if DD_TRACE_AGENT_URL is UDS
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }
#else
    [Theory]
    [InlineData(@"C:\temp\someval")]
    public void UnixAgentUriOnWindows_UdsUnsupported_UsesDefaultHttp(string path)
    {
        var settingsFromSource = Setup(FileExistsMock(path.Replace("\\", "/")), $"DD_TRACE_AGENT_URL:unix://{path}");
        var expectedUri = new Uri($"http://127.0.0.1:8126");
        AssertHttpIsConfigured(settingsFromSource, expectedUri);
        settingsFromSource.AgentUri.Should().Be(expectedUri);
        settingsFromSource.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        // Without additional settings, metrics defaults to UDP even if DD_TRACE_AGENT_URL is UDS
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }
#endif

    [Fact]
    public void InvalidAgentUrlShouldNotThrow()
    {
        var param = "http://Invalid=%Url!!";
        var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
        CheckDefaultValues(settingsFromSource);
        settingsFromSource.ValidationWarnings.Should().Contain($"The Uri: '{param}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData("unix://some/socket.soc", "/socket.soc")]
    [InlineData("unix://./socket.soc", "/socket.soc")]
    public void RelativeAgentUrlShouldWarn(string param, string expectedSocket)
    {
        var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
        settingsFromSource.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
        settingsFromSource.TracesUnixDomainSocketPath.Should().Be(expectedSocket);
        Assert.Equal(new Uri(param), settingsFromSource.AgentUri);
        CheckDefaultValues(settingsFromSource, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        settingsFromSource.ValidationWarnings.Should().Contain($"The provided Uri {param} contains a relative path which may not work. This is the path to the socket that will be used: /socket.soc");
    }

    [Theory]
    [InlineData("some/socket.soc", "/socket.soc")]
    [InlineData("./socket.soc", "/socket.soc")]
    public void RelativeDomainSocketShouldWarn(string param, string expectedSocket)
    {
        var settingsFromSource = Setup("DD_APM_RECEIVER_SOCKET", param);
        var uri = new Uri(ExporterSettings.UnixDomainSocketPrefix + param);

        settingsFromSource.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
        settingsFromSource.TracesUnixDomainSocketPath.Should().Be(expectedSocket);
        settingsFromSource.AgentUri.Should().Be(uri);
        CheckDefaultValues(settingsFromSource, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        settingsFromSource.ValidationWarnings.Should().Contain($"The provided Uri {uri.AbsoluteUri} contains a relative path which may not work. This is the path to the socket that will be used: {expectedSocket}");
        settingsFromSource.ValidationWarnings.Should().Contain($"The socket provided {expectedSocket} cannot be found. The tracer will still rely on this socket to send traces.");
    }
#endif

    [Fact]
    public void AgentHost()
    {
        var param = "SomeHost";
        var settingsFromSource = Setup("DD_AGENT_HOST", param);

        AssertHttpIsConfigured(settingsFromSource, new Uri("http://SomeHost:8126"));
        AssertMetricsUdpIsConfigured(settingsFromSource, "SomeHost");
    }

    [Fact]
    public void AgentPort()
    {
        var param = 9333;
        var settingsFromSource = Setup("DD_TRACE_AGENT_PORT", param.ToString());

        AssertHttpIsConfigured(settingsFromSource, new Uri("http://127.0.0.1:9333"));
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }

    [Fact]
    public void TracesPipeName()
    {
        var param = @"C:\temp\someval";
        var settingsFromSource = Setup("DD_TRACE_PIPE_NAME", param);

        AssertPipeIsConfigured(settingsFromSource, param);
        // metrics default to UDP
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void UnixDomainSocketPathWellFormed()
    {
        var settingsFromSource = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
        AssertUdsIsConfigured(settingsFromSource, "/var/datadog/myscocket.soc");
        AssertMetricsUdpIsConfigured(settingsFromSource);

        var settings = new ExporterSettings();
        AssertHttpIsConfigured(settings, new Uri("http://127.0.0.1:8126/"));
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }

    [Fact]
    public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
    {
        var settings = Setup(DefaultTraceSocketFilesExist());
        AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket);
        // Uses UDP by default
        AssertMetricsUdpIsConfigured(settings);
    }
#else
    [Fact]
    public void UnixDomainSocketPathWellFormed_UdsUnsupported_UsesDefaultHttp()
    {
        var settingsFromSource = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
        var expectedUri = new Uri("http://127.0.0.1:8126/");
        AssertHttpIsConfigured(settingsFromSource, expectedUri);
        AssertMetricsUdpIsConfigured(settingsFromSource);
        settingsFromSource.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");

        var settings = new ExporterSettings();
        AssertHttpIsConfigured(settings, expectedUri);
        AssertMetricsUdpIsConfigured(settingsFromSource);
    }

    [Fact]
    public void Traces_SocketFilesExist_NoExplicitConfig_UdsUnsupported_UsesDefaultTcp()
    {
        var expectedUri = new Uri($"http://127.0.0.1:8126");
        var settings = Setup(DefaultTraceSocketFilesExist());
        AssertHttpIsConfigured(settings, expectedUri);
        // Uses UDP by default
        AssertMetricsUdpIsConfigured(settings);
    }
#endif

    [Fact]
    public void Traces_SocketFilesExist_ExplicitAgentHost_UsesDefaultTcp()
    {
        var agentHost = "someotherhost";
        var expectedUri = new Uri($"http://{agentHost}:8126");
        var settings = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost");
        AssertHttpIsConfigured(settings, expectedUri);
        AssertMetricsUdpIsConfigured(settings, "someotherhost");
    }

    [Fact]
    public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultTcp()
    {
        var expectedUri = new Uri($"http://127.0.0.1:8111");
        var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_PORT:8111");
        AssertHttpIsConfigured(settings, expectedUri);
        AssertMetricsUdpIsConfigured(settings);
    }

    [Fact]
    public void Traces_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
    {
        var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe");
        AssertPipeIsConfigured(settings, "somepipe");
        // Metrics defaults to UDP
        AssertMetricsUdpIsConfigured(settings);
    }

    /// <summary>
    /// This test is not actually important for functionality, it is just to document existing behavior.
    /// If for some reason the priority needs to change in the future, there is no compelling reason why this test can't change.
    /// </summary>
    [Fact]
    public void Traces_SocketFilesExist_ExplicitConfigForWindowsPipeAndUdp_PrioritizesWindowsPipe()
    {
        var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
        AssertPipeIsConfigured(settings, "somepipe");
        // Metrics defaults to udp
        AssertMetricsUdpIsConfigured(settings);
    }

    [Fact]
    public void Traces_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
    {
        var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_URL:http://toto:1234", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
        AssertHttpIsConfigured(settings, new Uri("http://toto:1234"));
        AssertMetricsUdpIsConfigured(settings, "toto");
    }

    [Fact]
    public void OnlyHasReadOnlyProperties()
    {
        var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        var type = typeof(ExporterSettings);

        using var scope = new AssertionScope();

        var properties = type.GetProperties(flags);
        foreach (var propertyInfo in properties)
        {
            propertyInfo.CanWrite.Should().BeFalse($"{propertyInfo.Name} should be read only");
        }

        var fields = type.GetFields(flags);
        foreach (var field in fields)
        {
            var isReadonlyOrConstant = field.IsInitOnly || field.IsLiteral;
            isReadonlyOrConstant.Should().BeTrue($"{field.Name} should be read only");
        }
    }

    private ExporterSettings Setup(string key, string value)
    {
        return Setup(BuildSource(key + ":" + value), NoFile());
    }

    private ExporterSettings Setup(Func<string, bool> fileExistsMock, params string[] config)
    {
        return Setup(BuildSource(config), fileExistsMock);
    }

    private NameValueConfigurationSource BuildSource(params string[] config)
    {
        var configNameValues = new NameValueCollection();

        foreach (var item in config)
        {
            var separatorIndex = item.IndexOf(':');
            configNameValues.Add(item.Substring(0, separatorIndex), item.Substring(separatorIndex + 1));
        }

        return new NameValueConfigurationSource(configNameValues);
    }

    private void AssertHttpIsConfigured(ExporterSettings settings, Uri expectedUri)
    {
        settings.TracesTransport.Should().Be(TracesTransportType.Default);
        settings.AgentUri.Should().Be(expectedUri);
        settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
        CheckDefaultValues(settings, "AgentUri", "TracesTransport", "MetricsHostname");
    }

    private void AssertUdsIsConfigured(ExporterSettings settings, string socketPath)
    {
        settings.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
        settings.TracesUnixDomainSocketPath.Should().Be(socketPath);
        settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
        CheckDefaultValues(settings, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
    }

    private void AssertPipeIsConfigured(ExporterSettings settings, string pipeName)
    {
        settings.TracesTransport.Should().Be(TracesTransportType.WindowsNamedPipe);
        settings.TracesPipeName.Should().Be(pipeName);
        settings.AgentUri.Should().NotBeNull();
        settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
        CheckDefaultValues(settings, "TracesPipeName", "AgentUri", "TracesTransport", "TracesPipeTimeoutMs");
    }

    private Func<string, bool> NoFile()
    {
        return (f) => false;
    }

    private Func<string, bool> DefaultTraceSocketFilesExist()
    {
        return FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket);
    }

    private Func<string, bool> FileExistsMock(params string[] existingFiles)
    {
        return (f) =>
        {
            return existingFiles.Contains(f);
        };
    }

    private void CheckDefaultValues(ExporterSettings settings, params string[] paramToIgnore)
    {
        CheckSharedDefaultValues(settings, paramToIgnore);
        CheckSpecificDefaultValues(settings, paramToIgnore);
    }

    private void CheckSharedDefaultValues(ExporterSettings settings, string[] paramToIgnore)
    {
        if (!paramToIgnore.Contains("AgentUri"))
        {
            settings.AgentUri.AbsoluteUri.Should().Be("http://127.0.0.1:8126/");
        }

        if (!paramToIgnore.Contains("TracesTransport"))
        {
            settings.TracesTransport.Should().Be(TracesTransportType.Default);
        }

        if (!paramToIgnore.Contains("TracesPipeName"))
        {
            settings.TracesPipeName.Should().BeNull();
        }

        if (!paramToIgnore.Contains("TracesUnixDomainSocketPath"))
        {
            settings.TracesUnixDomainSocketPath.Should().BeNull();
        }
    }
}
