// <copyright file="HooksHandlers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    internal class HooksHandlers
    {
        private const string TEST_FRAMEWORK = "gauge-dotnet";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HookRegistryCtorIntegration));
        private static readonly ThreadLocal<ContextState> _state = new ThreadLocal<ContextState>(() => new ContextState());

        static HooksHandlers()
        {
            // Gauge uses a custom TaskScheduler that affects the Tracer initialization creating a deadlock.
            // So we force the tracer initialization in the default scheduler.
            Task.Factory.StartNew(() => Common.TestTracer, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Wait();
        }

        public static IExecutionContext ExecutionContext
        {
            get => _state.Value.ExecutionContext;
            set => _state.Value.ExecutionContext = value;
        }

        public static Span ScenarioSpan
        {
            get => _state.Value.ScenarioSpan;
            private set => _state.Value.ScenarioSpan = value;
        }

        public static Span StepSpan
        {
            get => _state.Value.StepSpan;
            private set => _state.Value.StepSpan = value;
        }

        public void BeforeScenario()
        {
            try
            {
                var context = ExecutionContext;
                var testSuite = context.CurrentSpecification.Name;
                var testName = context.CurrentScenario.Name;

                var span = Common.TestTracer.StartSpan("gauge.scenario", serviceName: CIEnvironmentValues.Repository, ignoreActiveScope: true);
                span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
                span.Type = SpanTypes.Test;
                span.ResourceName = $"{testSuite}.{testName}";
                span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
                span.SetTag(TestTags.Suite, testSuite);
                span.SetTag(TestTags.Name, testName);
                span.SetTag(TestTags.Framework, TEST_FRAMEWORK);
                span.SetTag(TestTags.Type, TestTags.TypeTest);
                CIEnvironmentValues.DecorateSpan(span);

                var framework = FrameworkDescription.Instance;

                span.SetTag(CommonTags.RuntimeName, framework.Name);
                span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);
                span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
                span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
                span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
                span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

                ScenarioSpan = span;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        public void AfterScenario()
        {
            try
            {
                var spanScenario = ScenarioSpan;
                var context = ExecutionContext;

                if (context.CurrentScenario.IsFailing)
                {
                    spanScenario.SetTag(TestTags.Status, TestTags.StatusFail);
                }
                else
                {
                    spanScenario.SetTag(TestTags.Status, TestTags.StatusPass);
                }

                spanScenario.Dispose();
                ScenarioSpan = null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        public void BeforeStep()
        {
            try
            {
                var spanScenario = ScenarioSpan;
                var context = ExecutionContext;
                var testSuite = context.CurrentSpecification.Name;
                var testName = context.CurrentScenario.Name + "/" + context.CurrentStep.Text;

                var span = Common.TestTracer.StartSpan("gauge.step", serviceName: CIEnvironmentValues.Repository, parent: spanScenario.Context);
                span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
                span.Type = SpanTypes.Test;
                span.ResourceName = $"{testSuite}.{testName}";
                span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
                span.SetTag(TestTags.Suite, testSuite);
                span.SetTag(TestTags.Name, testName);
                span.SetTag(TestTags.Framework, TEST_FRAMEWORK);
                span.SetTag(TestTags.Type, TestTags.TypeTest);
                CIEnvironmentValues.DecorateSpan(span);

                var framework = FrameworkDescription.Instance;

                span.SetTag(CommonTags.RuntimeName, framework.Name);
                span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);
                span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
                span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
                span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
                span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

                StepSpan = span;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        public void AfterStep()
        {
            try
            {
                var spanStep = StepSpan;
                var context = ExecutionContext;

                if (context.CurrentStep.IsFailing)
                {
                    spanStep.SetTag(TestTags.Status, TestTags.StatusFail);
                }
                else
                {
                    spanStep.SetTag(TestTags.Status, TestTags.StatusPass);
                }

                spanStep.Dispose();
                StepSpan = null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        private class ContextState
        {
            public IExecutionContext ExecutionContext { get; set; }

            public Span ScenarioSpan { get; set; }

            public Span StepSpan { get; set; }
        }
    }
}
