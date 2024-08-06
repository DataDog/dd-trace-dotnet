// <copyright file="CiRunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Internal;
using Datadog.Trace.Internal.Ci;
using Datadog.Trace.Internal.Ci.Tags;
using Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Internal.Util;
using Datadog.Trace.Internal.Vendors.Newtonsoft.Json;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    [EnvironmentVariablesCleaner(
        Internal.Configuration.ConfigurationKeys.AgentUri,
        Internal.Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath,
        Internal.Configuration.ConfigurationKeys.AgentHost,
        Internal.Configuration.ConfigurationKeys.AgentPort,
        Internal.Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled,
        Internal.Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy,
        Internal.Configuration.ConfigurationKeys.CIVisibility.CodeCoverage,
        Internal.Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath)]
    public class CiRunCommandTests : BaseRunCommandTests
    {
        public CiRunCommandTests()
            : base("ci run", enableCiVisibilityMode: true)
        {
            CIVisibility.UseLockedTracerManager = false;
        }

        [Fact]
        public void CoberturaCodeCoverage()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(directory, "coverage.cobertura.xml");
            RunExternalCoverageTest(filePath);
        }

        [Fact]
        public void OpenCoverCodeCoverage()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(directory, "coverage.opencover.xml");
            RunExternalCoverageTest(filePath);
        }

        private void RunExternalCoverageTest(string filePath)
        {
            CIVisibility.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Internal.Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool callbackInvoked = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                var session = DotnetCommon.CreateSession();
                command = c;
                arguments = a;
                environmentVariables = e;
                callbackInvoked = true;
                DotnetCommon.FinalizeSession(session, 0, null);
            };

            // CI visibility mode checks if there's a running agent
            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            MockCIVisibilityTestModule testSession = null;
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
                if (args.Value.Headers["Content-Type"] != "application/msgpack")
                {
                    return;
                }

                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(args.Value.BodyInJson);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Type == SpanTypes.TestSession)
                        {
                            testSession = JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(@event.Content.ToString());
                            break;
                        }
                    }
                }
            };

            EnvironmentHelpers.SetEnvironmentVariable(Internal.Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);

            var agentUrl = $"http://localhost:{agent.Port}";
            var commandLine = $"{CommandPrefix} test.exe --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env VAR1=A --set-env VAR2=B";

            using var console = ConsoleHelper.Redirect();

            var exitCode = Program.Main(commandLine.Split(' '));

            using var scope = new AssertionScope();

            scope.AddReportable("output", console.Output);

            exitCode.Should().Be(0);
            callbackInvoked.Should().BeTrue();

            command.Should().Be("test.exe");
            arguments.Should().BeNullOrEmpty();
            environmentVariables.Should().NotBeNull();

            testSession.Should().NotBeNull();
            testSession.Meta.Should().Contain(new KeyValuePair<string, string>(CodeCoverageTags.Enabled, "true"));
            testSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 83.33));
        }
    }
}
