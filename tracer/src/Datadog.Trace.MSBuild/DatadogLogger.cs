// <copyright file="DatadogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Logging.DirectSubmission;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Microsoft.Build.Framework;

namespace Datadog.Trace.MSBuild
{
    /// <summary>
    /// Build logger
    /// </summary>
    public class DatadogLogger : INodeLogger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogLogger));

        private readonly ConcurrentDictionary<int, object> _projects = new();
        private TestSession? _testSession;
        private TestModule? _testModule;
        private TestSuite? _testSuite;
        private bool _buildError;

        static DatadogLogger()
        {
            try
            {
                Environment.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "0", EnvironmentVariableTarget.Process);
            }
            catch
            {
                // .
            }

            CIVisibility.Initialize();
        }

        /// <summary>
        /// Gets or sets the logger Verbosity
        /// </summary>
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

        /// <summary>
        /// Gets or sets the logger Parameters
        /// </summary>
        public string? Parameters { get; set; }

        /// <inheritdoc />
        public void Initialize(IEventSource? eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        /// <inheritdoc />
        public void Initialize(IEventSource? eventSource)
        {
            if (eventSource == null)
            {
                return;
            }

            try
            {
                // Attach to the eventSource events only if we successfully get the tracer instance.
                eventSource.BuildStarted += EventSource_BuildStarted;
                eventSource.BuildFinished += EventSource_BuildFinished;
                eventSource.ProjectStarted += EventSource_ProjectStarted;
                eventSource.ProjectFinished += EventSource_ProjectFinished;
                eventSource.ErrorRaised += EventSource_ErrorRaised;
                eventSource.WarningRaised += EventSource_WarningRaised;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing the logger.");
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
        }

        private void EventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
            try
            {
                Log.Debug("Build Started");
                _testSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "MSBuild");
                _testModule = _testSession.CreateModule(BuildTags.BuildOperationName, "MSBuild", string.Empty);
                _testModule.SetTag(BuildTags.BuildName, e.SenderName);
                _testModule.SetTag(BuildTags.BuildCommand, Environment.CommandLine);
                _testModule.SetTag(BuildTags.BuildWorkingFolder, Environment.CurrentDirectory);
                Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, _testModule.GetInternalSpan()));
                _testSuite = _testModule.GetOrCreateSuite(e.SenderName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in BuildStarted event");
            }
        }

        private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            try
            {
                Log.Debug("Build Finished");
                if (_testSession is null || _testModule is null || _testSuite is null)
                {
                    return;
                }

                _testModule.Tags.Status = TestTags.StatusPass;
                if (!e.Succeeded)
                {
                    _buildError = true;
                    _testModule.Tags.Status = TestTags.StatusFail;
                    _testSession.SetErrorInfo(null, null, null);
                    _testModule.SetErrorInfo(null, null, null);
                    _testSuite.SetErrorInfo(null, null, null);
                }

                _testSuite.Close();
                Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, _testModule.GetInternalSpan()));
                _testModule.Close();
                _testSession.Close(_buildError ? TestStatus.Fail : TestStatus.Pass);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in BuildFinished event");
            }
        }

        private void EventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            try
            {
                Log.Debug("Project Started");

                var parentContext = e.ParentProjectBuildEventContext.ProjectContextId;
                var context = e.BuildEventContext.ProjectContextId;
                var projectName = !string.IsNullOrEmpty(e.ProjectFile) ? Path.GetFileName(e.ProjectFile) : null;
                var targetName = string.IsNullOrEmpty(e.TargetNames) ? "build" : e.TargetNames?.ToLowerInvariant() ?? string.Empty;

                if (_projects.TryGetValue(parentContext, out var parentContextObject))
                {
                    var parentSpan = parentContextObject is Test { } test ? test.GetInternalSpan() : (Span)parentContextObject;
                    var projectSpan = Tracer.Instance.StartSpan(targetName, parent: parentSpan.Context);
                    if (projectName != null)
                    {
                        projectSpan.ServiceName = projectName;
                    }

                    projectSpan.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
                    projectSpan.Type = SpanTypes.Build;

                    string? targetFramework = null;
                    foreach (KeyValuePair<string, string> prop in e.GlobalProperties)
                    {
                        projectSpan.SetTag($"{BuildTags.ProjectProperties}.{prop.Key}", prop.Value);
                        if (string.Equals(prop.Key, "TargetFramework", StringComparison.OrdinalIgnoreCase))
                        {
                            targetFramework = prop.Value;
                        }
                    }

                    projectSpan.ResourceName = (string.IsNullOrEmpty(e.TargetNames) ? "Build" : e.TargetNames) + $"/{projectName}/{targetFramework}";
                    projectSpan.SetTag(BuildTags.ProjectFile, e.ProjectFile);
                    projectSpan.SetTag(BuildTags.ProjectSenderName, e.SenderName);
                    projectSpan.SetTag(BuildTags.ProjectTargetNames, e.TargetNames);
                    projectSpan.SetTag(BuildTags.ProjectToolsVersion, e.ToolsVersion);
                    projectSpan.SetTag(BuildTags.BuildName, projectName);
                    Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, projectSpan));
                    _projects.TryAdd(context, projectSpan);
                }
                else if (_testSuite is { } testSuite)
                {
                    e.GlobalProperties.TryGetValue("Configuration", out var configuration);
                    e.GlobalProperties.TryGetValue("TargetFramework", out var targetFramework);
                    var name = $"{projectName}_{configuration}_{targetName}";
                    var test = testSuite.CreateTest(name);
                    foreach (KeyValuePair<string, string> prop in e.GlobalProperties)
                    {
                        test.SetTag($"{BuildTags.ProjectProperties}.{prop.Key}", prop.Value);
                    }

                    test.SetTag(BuildTags.ProjectFile, e.ProjectFile);
                    test.SetTag(BuildTags.ProjectSenderName, e.SenderName);
                    test.SetTag(BuildTags.ProjectTargetNames, e.TargetNames);
                    test.SetTag(BuildTags.ProjectToolsVersion, e.ToolsVersion);
                    test.SetTag(BuildTags.BuildName, projectName);
                    Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, test.GetInternalSpan()));
                    _projects.TryAdd(context, test);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ProjectStarted event");
            }
        }

        private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            try
            {
                var context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryRemove(context, out var projectObject))
                {
                    Log.Debug("Project Finished");

                    if (projectObject is Span { } projectSpan)
                    {
                        projectSpan.SetTag(BuildTags.BuildStatus, e.Succeeded ? BuildTags.BuildSucceededStatus : BuildTags.BuildFailedStatus);
                        if (!e.Succeeded)
                        {
                            projectSpan.Error = true;
                        }

                        Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, projectSpan));
                        projectSpan.Finish();
                        _projects.TryRemove(context, out _);
                    }
                    else if (projectObject is Test { } projectTest)
                    {
                        if (!e.Succeeded)
                        {
                            projectTest.SetErrorInfo(null, null, null);
                        }

                        Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, projectTest.GetInternalSpan()));
                        projectTest.Close(e.Succeeded ? TestStatus.Pass : TestStatus.Fail);
                        _projects.TryRemove(context, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ProjectFinished event");
            }
        }

        private void EventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            try
            {
                if (e.BuildEventContext == null)
                {
                    return;
                }

                Log.Debug("Error Raised");

                var context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryGetValue(context, out var projectObject))
                {
                    var projectSpan = projectObject is Test { } projectTest ? projectTest.GetInternalSpan() : (Span)projectObject;
                    projectSpan.Error = true;
                    projectSpan.SetTag(BuildTags.ErrorMessage, e.Message);
                    projectSpan.SetTag(BuildTags.ErrorType, $"{e.SenderName?.ToUpperInvariant()} ({e.Code}) Error");
                    projectSpan.SetTag(BuildTags.ErrorCode, e.Code);
                    projectSpan.SetTag(BuildTags.ErrorProjectFile, e.ProjectFile);

                    if (!string.IsNullOrEmpty(e.File))
                    {
                        var filePath = Path.Combine(Path.GetDirectoryName(e.ProjectFile) ?? string.Empty, e.File);
                        projectSpan.SetTag(BuildTags.ErrorFile, filePath);
                        if (e.LineNumber > 0)
                        {
                            projectSpan.SetTag(BuildTags.ErrorStack, $" at Source code in {filePath}:line {e.LineNumber}");
                        }
                    }

                    if (!string.IsNullOrEmpty(e.Subcategory))
                    {
                        projectSpan.SetTag(BuildTags.ErrorSubCategory, e.Subcategory);
                    }

                    if (e.LineNumber > 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorStartLine, e.LineNumber.ToString());
                    }

                    if (e.ColumnNumber > 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorStartColumn, e.ColumnNumber.ToString());
                    }

                    if (e.EndLineNumber > 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorEndLine, e.EndLineNumber.ToString());
                    }

                    if (e.EndColumnNumber > 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorEndColumn, e.EndColumnNumber.ToString());
                    }

                    Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "info", e.Message, projectSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ErrorRaised event");
            }
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            try
            {
                if (e.BuildEventContext == null)
                {
                    return;
                }

                Log.Debug("Warning Raised");

                var context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryGetValue(context, out var projectObject))
                {
                    var projectSpan = projectObject is Test { } projectTest ? projectTest.GetInternalSpan() : (Span)projectObject;
                    Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("MSBuild", "warning", e.Message, projectSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in WarningRaised event");
            }
        }
    }
}
