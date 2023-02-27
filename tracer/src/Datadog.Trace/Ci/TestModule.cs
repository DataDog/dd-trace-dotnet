// <copyright file="TestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test module
/// </summary>
public sealed class TestModule
{
    private static readonly AsyncLocal<TestModule?> CurrentModule = new();
    private readonly Span _span;
    private readonly Dictionary<string, TestSuite> _suites;
    private readonly TestSession? _fakeSession;
    private int _finished;

    private TestModule(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate)
        : this(name, framework, frameworkVersion, startDate, null)
    {
    }

    internal TestModule(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate, TestSessionSpanTags? sessionSpanTags)
    {
        // First we make sure that CI Visibility is initialized.
        CIVisibility.InitializeFromManualInstrumentation();

        var environment = CIEnvironmentValues.Instance;
        var frameworkDescription = FrameworkDescription.Instance;
        _suites = new Dictionary<string, TestSuite>();

        Name = name;
        Framework = framework;

        // if sessionSpanTags is not null then the TestSession is in-proc.
        // otherwise the TestSession is out of process

        TestModuleSpanTags tags;
        if (sessionSpanTags is not null)
        {
            // In-Proc session
            tags = new TestModuleSpanTags
            {
                Type = TestTags.TypeTest,
                Module = name,
                Framework = framework,
                FrameworkVersion = frameworkVersion,
                CIProvider = sessionSpanTags.CIProvider,
                CIPipelineId = sessionSpanTags.CIPipelineId,
                CIPipelineName = sessionSpanTags.CIPipelineName,
                CIPipelineNumber = sessionSpanTags.CIPipelineNumber,
                CIPipelineUrl = sessionSpanTags.CIPipelineUrl,
                CIJobName = sessionSpanTags.CIJobName,
                CIJobUrl = sessionSpanTags.CIJobUrl,
                StageName = sessionSpanTags.StageName,
                CIWorkspacePath = sessionSpanTags.CIWorkspacePath,
                GitRepository = sessionSpanTags.GitRepository,
                GitCommit = sessionSpanTags.GitCommit,
                GitBranch = sessionSpanTags.GitBranch,
                GitTag = sessionSpanTags.GitTag,
                GitCommitAuthorDate = sessionSpanTags.GitCommitAuthorDate,
                GitCommitAuthorName = sessionSpanTags.GitCommitAuthorName,
                GitCommitAuthorEmail = sessionSpanTags.GitCommitAuthorEmail,
                GitCommitCommitterDate = sessionSpanTags.GitCommitCommitterDate,
                GitCommitCommitterName = sessionSpanTags.GitCommitCommitterName,
                GitCommitCommitterEmail = sessionSpanTags.GitCommitCommitterEmail,
                GitCommitMessage = sessionSpanTags.GitCommitMessage,
                BuildSourceRoot = sessionSpanTags.BuildSourceRoot,
                RuntimeName = frameworkDescription.Name,
                RuntimeVersion = frameworkDescription.ProductVersion,
                RuntimeArchitecture = frameworkDescription.ProcessArchitecture,
                OSArchitecture = frameworkDescription.OSArchitecture,
                OSPlatform = frameworkDescription.OSPlatform,
                OSVersion = CIVisibility.GetOperatingSystemVersion(),
                CiEnvVars = sessionSpanTags.CiEnvVars,
                SessionId = sessionSpanTags.SessionId,
                Command = sessionSpanTags.Command,
                WorkingDirectory = sessionSpanTags.WorkingDirectory,
            };
        }
        else
        {
            // Out-of-Proc session
            tags = new TestModuleSpanTags
            {
                Type = TestTags.TypeTest,
                Module = name,
                Framework = framework,
                FrameworkVersion = frameworkVersion,
                RuntimeName = frameworkDescription.Name,
                RuntimeVersion = frameworkDescription.ProductVersion,
                RuntimeArchitecture = frameworkDescription.ProcessArchitecture,
                OSArchitecture = frameworkDescription.OSArchitecture,
                OSPlatform = frameworkDescription.OSPlatform,
                OSVersion = CIVisibility.GetOperatingSystemVersion(),
            };

            tags.SetCIEnvironmentValues(environment);

            // Extract session variables (from out of process sessions)
            var environmentVariables = EnvironmentHelpers.GetEnvironmentVariables();
            var sessionContext = SpanContextPropagator.Instance.Extract(
                environmentVariables, new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (sessionContext is not null)
            {
                tags.SessionId = sessionContext.SpanId;
                if (environmentVariables.TryGetValue<string>(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, out var testSessionCommand))
                {
                    tags.Command = testSessionCommand;
                }

                if (environmentVariables.TryGetValue<string>(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable, out var testSessionWorkingDirectory))
                {
                    tags.WorkingDirectory = testSessionWorkingDirectory;
                }
            }
            else
            {
                Log.Information("A session cannot be found, creating a fake session as a parent of the module.");
                _fakeSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, null, startDate, false);
                if (_fakeSession.Tags is { } fakeSessionTags)
                {
                    tags.SessionId = fakeSessionTags.SessionId;
                    tags.Command = fakeSessionTags.Command;
                    tags.WorkingDirectory = fakeSessionTags.WorkingDirectory;
                }
            }
        }

        // Check if Intelligent Test Runner has skippable tests and set the flag according to that
        tags.TestsSkipped = CIVisibility.HasSkippableTests() ? "true" : "false";

        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(framework) ? "test_module" : $"{framework!.ToLowerInvariant()}.test_module",
            tags: tags,
            startTime: startDate);

        span.Type = SpanTypes.TestModule;
        span.ResourceName = name;
        span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep);
        span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;

        tags.ModuleId = span.SpanId;

        _span = span;
        Current = this;
        CIVisibility.Log.Debug("### Test Module Created: {Name}", name);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            span.ResetStartTime();
        }
    }

    /// <summary>
    /// Gets the module name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test module start date
    /// </summary>
    public DateTimeOffset StartTime => _span.StartTime;

    /// <summary>
    /// Gets the test framework
    /// </summary>
    public string? Framework { get; }

    /// <summary>
    /// Gets or sets the current TestModule
    /// </summary>
    internal static TestModule? Current
    {
        get => CurrentModule.Value;
        set => CurrentModule.Value = value;
    }

    internal TestModuleSpanTags Tags => (TestModuleSpanTags)_span.Tags;

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    public static TestModule Create(string name)
    {
        return new TestModule(name, null, null, null);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    public static TestModule Create(string name, string framework, string frameworkVersion)
    {
        return new TestModule(name, framework, frameworkVersion, null);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    public static TestModule Create(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        return new TestModule(name, framework, frameworkVersion, startDate);
    }

    /// <summary>
    /// Sets a string tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, string? value)
    {
        _span.SetTag(key, value);
    }

    /// <summary>
    /// Sets a number tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, double? value)
    {
        _span.SetMetric(key, value);
    }

    /// <summary>
    /// Set Error Info
    /// </summary>
    /// <param name="type">Error type</param>
    /// <param name="message">Error message</param>
    /// <param name="callStack">Error callstack</param>
    public void SetErrorInfo(string type, string message, string? callStack)
    {
        var span = _span;
        span.Error = true;
        span.SetTag(Trace.Tags.ErrorType, type);
        span.SetTag(Trace.Tags.ErrorMsg, message);
        if (callStack is not null)
        {
            span.SetTag(Trace.Tags.ErrorStack, callStack);
        }
    }

    /// <summary>
    /// Set Error Info from Exception
    /// </summary>
    /// <param name="exception">Exception instance</param>
    public void SetErrorInfo(Exception exception)
    {
        _span.SetException(exception);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <remarks>Use CloseAsync() version whenever possible.</remarks>
    public void Close()
    {
        Close(null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <remarks>Use CloseAsync() version whenever possible.</remarks>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TimeSpan? duration)
    {
        if (InternalClose(duration))
        {
            CIVisibility.Log.Debug("### Test Module Flushing after close: {Name}", Name);
            CIVisibility.Flush();
        }
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <returns>Task instance </returns>
    public Task CloseAsync()
    {
        return CloseAsync(null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    /// <returns>Task instance </returns>
    public Task CloseAsync(TimeSpan? duration)
    {
        if (InternalClose(duration))
        {
            CIVisibility.Log.Debug("### Test Module Flushing after close: {Name}", Name);
            return CIVisibility.FlushAsync();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    private bool InternalClose(TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return false;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

        var remainingSuites = Array.Empty<TestSuite>();
        lock (_suites)
        {
            if (_suites.Count > 0)
            {
                remainingSuites = _suites.Values.ToArray();
            }
        }

        foreach (var suite in remainingSuites)
        {
            suite.Close();
        }

        // Update status
        Tags.Status ??= TestTags.StatusPass;

        if (CIVisibility.Settings.CodeCoverageEnabled == true &&
            CoverageReporter.Handler is DefaultWithGlobalCoverageEventHandler coverageHandler &&
            coverageHandler.GetCodeCoveragePercentage() is { } globalCoverage)
        {
            // We only report global code coverage if we don't skip any test
            if (!CIVisibility.HasSkippableTests())
            {
                // Adds the global code coverage percentage to the module
                span.SetTag(CommonTags.CodeCoverageTotalLines, globalCoverage.Data[0].ToString(CultureInfo.InvariantCulture));
            }

            // If the code coverage path environment variable is set, we store the json file
            if (!string.IsNullOrWhiteSpace(CIVisibility.Settings.CodeCoveragePath))
            {
                var codeCoveragePath = Path.Combine(CIVisibility.Settings.CodeCoveragePath, $"coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-{Guid.NewGuid():n}.json");
                try
                {
                    using var fStream = File.OpenWrite(codeCoveragePath);
                    using var sWriter = new StreamWriter(fStream, Encoding.UTF8, 4096, false);
                    new JsonSerializer().Serialize(sWriter, globalCoverage);
                }
                catch (Exception ex)
                {
                    CIVisibility.Log.Error(ex, "Error writing global code coverage.");
                }
            }
        }

        if (CIVisibility.Settings.TestsSkippingEnabled.HasValue)
        {
            span.SetTag(CommonTags.TestModuleTestsSkippingEnabled, CIVisibility.Settings.TestsSkippingEnabled.Value ? "true" : "false");
            span.SetTag(CommonTags.TestsSkipped, CIVisibility.HasSkippableTests() ? "true" : "false");
        }

        if (CIVisibility.Settings.CodeCoverageEnabled.HasValue)
        {
            span.SetTag(CommonTags.TestModuleCodeCoverageEnabled, CIVisibility.Settings.CodeCoverageEnabled.Value ? "true" : "false");
        }

        span.Finish(duration.Value);

        Current = null;
        CIVisibility.Log.Debug("### Test Module Closed: {Name} | {Status}", Name, Tags.Status);

        if (_fakeSession is { } fakeSession)
        {
            switch (Tags.Status)
            {
                case TestTags.StatusPass:
                    fakeSession.InternalClose(TestStatus.Pass, duration.Value);
                    break;
                case TestTags.StatusFail:
                    fakeSession.InternalClose(TestStatus.Fail, duration.Value);
                    break;
                case TestTags.StatusSkip:
                    fakeSession.InternalClose(TestStatus.Skip, duration.Value);
                    break;
                default:
                    fakeSession.InternalClose(TestStatus.Pass, duration.Value);
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    public TestSuite GetOrCreateSuite(string name)
    {
        return GetOrCreateSuite(name, null);
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>Test suite instance</returns>
    public TestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate)
    {
        lock (_suites)
        {
            if (_suites.TryGetValue(name, out var suite))
            {
                return suite;
            }

            suite = new TestSuite(this, name, startDate);
            _suites[name] = suite;
            return suite;
        }
    }

    /// <summary>
    /// Gets an existing test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    internal TestSuite? GetSuite(string name)
    {
        lock (_suites)
        {
            return _suites.TryGetValue(name, out var suite) ? suite : null;
        }
    }

    internal void RemoveSuite(string name)
    {
        lock (_suites)
        {
            _suites.Remove(name);
        }
    }
}
