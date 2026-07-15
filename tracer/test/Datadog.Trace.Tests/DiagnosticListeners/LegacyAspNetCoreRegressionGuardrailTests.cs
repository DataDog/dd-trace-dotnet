// <copyright file="LegacyAspNetCoreRegressionGuardrailTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class LegacyAspNetCoreRegressionGuardrailTests
    {
        [Fact]
        public void Net461TracerDoesNotReferenceAspNetCoreOrDiagnosticSource()
        {
            var tracerAssembly = typeof(Tracer).Assembly;
            var targetFramework = tracerAssembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false)
                                                .Cast<TargetFrameworkAttribute>()
                                                .Single();
            var references = tracerAssembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

            targetFramework.FrameworkName.Should().Be(".NETFramework,Version=v4.6.1");
            references.Should().NotContain(
                name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
                "the net461 tracer must use duck typing instead of compile-time ASP.NET Core references");
            references.Should().NotContain(
                "System.Diagnostics.DiagnosticSource",
                "the net461 tracer loads DiagnosticSource dynamically");
        }

        [Fact]
        public async Task DefaultDisabledFeatureDoesNotRegisterOrSubscribeObserver()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            AssertObserverIsNotRegisteredOrSubscribed(tracer);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task DisabledGateDoesNotRegisterOrSubscribeObserver(bool frameworkFeatureEnabled, bool aspNetCoreIntegrationEnabled)
        {
            var aspNetCoreEnabledKey = IntegrationNameToKeys.GetIntegrationEnabledKeys(nameof(IntegrationId.AspNetCore)).Key;
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, frameworkFeatureEnabled.ToString() },
                        { aspNetCoreEnabledKey, aspNetCoreIntegrationEnabled.ToString() },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            AssertObserverIsNotRegisteredOrSubscribed(tracer);
        }

        [Fact]
        public async Task DisabledFrameworkFeatureDoesNotLogStartupDiagnostic()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var logger = new Mock<IDatadogLogger>();
            Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();

            try
            {
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: false, diagnosticSourceAvailable: false, diagnosticSourceLoadException: null, logger.Object);
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: true, diagnosticSourceAvailable: true, diagnosticSourceLoadException: null, logger.Object);

                logger.Invocations.Should().BeEmpty();
            }
            finally
            {
                Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();
            }
        }

        [Fact]
        public async Task EnabledObserverLogsStartupDiagnosticOnce()
        {
            var settings = CreateSettings(frameworkFeatureEnabled: true, aspNetCoreIntegrationEnabled: true);
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var logger = new Mock<IDatadogLogger>();
            Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();

            try
            {
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: true, diagnosticSourceAvailable: true, diagnosticSourceLoadException: null, logger.Object);
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: true, diagnosticSourceAvailable: true, diagnosticSourceLoadException: null, logger.Object);

                logger.Invocations.Count(invocation => invocation.Method.Name == nameof(IDatadogLogger.Information)).Should().Be(1);
                logger.Invocations.Should().NotContain(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning));
            }
            finally
            {
                Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();
            }
        }

        [Theory]
        [InlineData(false, true, "disabled by DD_DIAGNOSTIC_SOURCE_ENABLED")]
        [InlineData(true, false, "could not be loaded")]
        public async Task EnabledFrameworkFeatureLogsDiagnosticSourceProblemOnce(
            bool diagnosticSourceEnabled,
            bool diagnosticSourceAvailable,
            string expectedMessage)
        {
            var settings = CreateSettings(frameworkFeatureEnabled: true, aspNetCoreIntegrationEnabled: true);
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var logger = new Mock<IDatadogLogger>();
            Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();

            try
            {
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled, diagnosticSourceAvailable, diagnosticSourceLoadException: null, logger.Object);
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled, diagnosticSourceAvailable, diagnosticSourceLoadException: null, logger.Object);
                Instrumentation.LogGenericDiagnosticSourceUnavailable(tracer, loadException: null, logger.Object);

                var warning = logger.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning));
                warning.Arguments[0].Should().BeOfType<string>().Which.Should().Contain(expectedMessage);
                logger.Invocations.Should().NotContain(invocation => invocation.Method.Name == nameof(IDatadogLogger.Information));
            }
            finally
            {
                Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MissingDiagnosticSourceWithDisabledFrameworkFeatureLogsGenericWarning(bool includeLoadException)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var logger = new Mock<IDatadogLogger>();
            var loadException = includeLoadException ? new FileLoadException("Test DiagnosticSource load failure") : null;

            Instrumentation.LogGenericDiagnosticSourceUnavailable(tracer, loadException, logger.Object);

            var warning = logger.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning));
            warning.Arguments.OfType<string>().Should().Contain(message => message.Contains("DiagnosticSource type could not be loaded"));
            if (includeLoadException)
            {
                warning.Arguments.Should().Contain(loadException);
            }
        }

        [Fact]
        public async Task DiagnosticSourceLoadExceptionLogsLegacyUnavailableOnce()
        {
            var settings = CreateSettings(frameworkFeatureEnabled: true, aspNetCoreIntegrationEnabled: true);
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var logger = new Mock<IDatadogLogger>();
            Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();

            try
            {
                const string InvalidAssemblyName = "System.Diagnostics.DiagnosticSource, System.Diagnostics.DiagnosticSource, Version=invalid";
                var diagnosticSourceType = Instrumentation.LoadDiagnosticSourceType(InvalidAssemblyName, out var loadException);

                diagnosticSourceType.Should().BeNull();
                loadException.Should().NotBeNull();

                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: true, diagnosticSourceType is not null, loadException, logger.Object);
                Instrumentation.LogLegacyAspNetCoreStartupDiagnostic(tracer, diagnosticSourceEnabled: true, diagnosticSourceType is not null, loadException, logger.Object);
                Instrumentation.LogGenericDiagnosticSourceUnavailable(tracer, loadException, logger.Object);

                var warning = logger.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning));
                warning.Arguments.Should().Contain(loadException);
                warning.Arguments.OfType<string>().Should().Contain(message => message.Contains("ASP.NET Core instrumentation for .NET Framework"));
            }
            finally
            {
                Instrumentation.ResetLegacyAspNetCoreStartupDiagnosticForTests();
            }
        }

        [Theory]
        [InlineData("DiagnosticListeners/AspNetCoreDiagnosticObserver.cs", "fea27348e128bf1e228478cfb3cbcef51844fce1db7920399b8f22c5d54708a5")]
        [InlineData("PlatformHelpers/AspNetCoreHttpRequestHandler.cs", "41c7d1b001e17bfa0605f36ed1f3051f0ce09dc3bcc0423400112e099291317b")]
        [InlineData("DiagnosticListeners/AspNetCoreResourceNameHelper.cs", "2d3349fcab00d7717553534fa494a01ea036e14634aab2618f6a9775eb078cd7")]
        public void ModernAspNetCoreSourceFileHasNotChanged(string relativePath, string expectedHash)
        {
            var sourcePath = Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "tracer",
                "src",
                "Datadog.Trace",
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var source = File.ReadAllText(sourcePath).Replace("\r\n", "\n").Replace('\r', '\n');

            using var sha256 = SHA256.Create();
            var actualHash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(source)))
                                         .Replace("-", string.Empty)
                                         .ToLowerInvariant();
            var because = "legacy ASP.NET Core changes must not modify the modern request path " +
                          $"({relativePath}); update this baseline only for a separately reviewed modern ASP.NET Core change";

            actualHash.Should().Be(expectedHash, because);
        }

        private static void AssertObserverIsNotRegisteredOrSubscribed(Tracer tracer)
        {
            var observers = new List<DiagnosticObserver>();
            Instrumentation.AddLegacyAspNetCoreDiagnosticObserverIfEnabled(observers, tracer);

            observers.Should().BeEmpty("a disabled gate must not construct or register the legacy observer");

            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Microsoft.AspNetCore");
            using var manager = new DiagnosticManager(observers);
            manager.OnNext(listener.Object);

            listener.Verify(
                instance => instance.Subscribe(
                    It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                    It.IsAny<Predicate<string>>()),
                Times.Never);
        }

        private static TracerSettings CreateSettings(bool frameworkFeatureEnabled, bool aspNetCoreIntegrationEnabled)
        {
            var aspNetCoreEnabledKey = IntegrationNameToKeys.GetIntegrationEnabledKeys(nameof(IntegrationId.AspNetCore)).Key;
            return new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, frameworkFeatureEnabled.ToString() },
                        { aspNetCoreEnabledKey, aspNetCoreIntegrationEnabled.ToString() },
                    }));
        }
    }
}

#endif
