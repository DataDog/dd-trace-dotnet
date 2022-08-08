// <copyright file="DatadogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Microsoft.Build.Framework;

namespace Datadog.Trace.MSBuild
{
    /// <summary>
    /// Build logger
    /// </summary>
    public class DatadogLogger : INodeLogger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogLogger));

        private Tracer _tracer = null;
        private Span _buildSpan = null;
        private ConcurrentDictionary<int, Span> _projects = new ConcurrentDictionary<int, Span>();

        static DatadogLogger()
        {
            try
            {
                Environment.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
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
        public string Parameters { get; set; }

        /// <summary>
        /// Gets or sets the Number of processors
        /// </summary>
        public int NumberOfProcessors { get; set; } = 1;

        /// <inheritdoc />
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            NumberOfProcessors = nodeCount;
            Initialize(eventSource);
        }

        /// <inheritdoc />
        public void Initialize(IEventSource eventSource)
        {
            if (eventSource == null)
            {
                return;
            }

            try
            {
                _tracer = Tracer.Instance;

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

                _buildSpan = _tracer.StartSpan(BuildTags.BuildOperationName);
                _buildSpan.SetMetric(Tags.Analytics, 1.0d);
                _buildSpan.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.AutoKeep);

                _buildSpan.Type = SpanTypes.Build;
                _buildSpan.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
                _buildSpan.SetTag(BuildTags.BuildName, e.SenderName);

                _buildSpan.SetTag(BuildTags.BuildCommand, Environment.CommandLine);
                _buildSpan.SetTag(BuildTags.BuildWorkingFolder, Environment.CurrentDirectory);

                _buildSpan.SetTag(CommonTags.OSArchitecture, Environment.Is64BitOperatingSystem ? "x64" : "x86");
                _buildSpan.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);
                _buildSpan.SetTag(CommonTags.RuntimeArchitecture, Environment.Is64BitProcess ? "x64" : "x86");
                _buildSpan.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
                CIEnvironmentValues.Instance.DecorateSpan(_buildSpan);

                _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Information, e.Message, _buildSpan));
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
                if (_buildSpan is null)
                {
                    return;
                }

                _buildSpan.SetTag(BuildTags.BuildStatus, e.Succeeded ? BuildTags.BuildSucceededStatus : BuildTags.BuildFailedStatus);
                if (!e.Succeeded)
                {
                    _buildSpan.Error = true;
                }

                _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Information, e.Message, _buildSpan));
                _buildSpan.Finish(e.Timestamp);
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
                if (e.TargetNames.StartsWith("_"))
                {
                    // Ignoring internal targetNames
                    return;
                }

                Log.Debug("Project Started");

                int parentContext = e.ParentProjectBuildEventContext.ProjectContextId;
                int context = e.BuildEventContext.ProjectContextId;
                if (!_projects.TryGetValue(parentContext, out Span parentSpan))
                {
                    parentSpan = _buildSpan;
                }

                string projectName = Path.GetFileName(e.ProjectFile);

                string targetName = string.IsNullOrEmpty(e.TargetNames) ? "build" : e.TargetNames?.ToLowerInvariant();
                Span projectSpan = _tracer.StartSpan($"msbuild.{targetName}", parent: parentSpan.Context);

                if (projectName != null)
                {
                    projectSpan.ServiceName = projectName;
                }

                projectSpan.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
                projectSpan.Type = SpanTypes.Build;

                string targetFramework = null;
                foreach (KeyValuePair<string, string> prop in e.GlobalProperties)
                {
                    projectSpan.SetTag($"{BuildTags.ProjectProperties}.{prop.Key}", prop.Value);
                    if (string.Equals(prop.Key, "TargetFramework", StringComparison.OrdinalIgnoreCase))
                    {
                        targetFramework = prop.Value;
                    }
                }

                projectSpan.ResourceName = (string.IsNullOrEmpty(e.TargetNames) ? "Build" : e.TargetNames) + $"/{targetFramework}";

                projectSpan.SetTag(BuildTags.ProjectFile, e.ProjectFile);
                projectSpan.SetTag(BuildTags.ProjectSenderName, e.SenderName);
                projectSpan.SetTag(BuildTags.ProjectTargetNames, e.TargetNames);
                projectSpan.SetTag(BuildTags.ProjectToolsVersion, e.ToolsVersion);
                projectSpan.SetTag(BuildTags.BuildName, projectName);
                _projects.TryAdd(context, projectSpan);
                _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Information, e.Message, projectSpan));
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
                int context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryRemove(context, out Span projectSpan))
                {
                    Log.Debug("Project Finished");

                    projectSpan.SetTag(BuildTags.BuildStatus, e.Succeeded ? BuildTags.BuildSucceededStatus : BuildTags.BuildFailedStatus);
                    if (!e.Succeeded)
                    {
                        projectSpan.Error = true;
                    }

                    _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Information, e.Message, projectSpan));
                    projectSpan.Finish(e.Timestamp);
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

                int context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryGetValue(context, out Span projectSpan))
                {
                    string correlation = $"[{CorrelationIdentifier.TraceIdKey}={projectSpan.TraceId},{CorrelationIdentifier.SpanIdKey}={projectSpan.SpanId}]";
                    string message = e.Message;
                    string type = $"{e.SenderName?.ToUpperInvariant()} ({e.Code}) Error";
                    string code = e.Code;
                    int? lineNumber = e.LineNumber > 0 ? (int?)e.LineNumber : null;
                    int? columnNumber = e.ColumnNumber > 0 ? (int?)e.ColumnNumber : null;
                    int? endLineNumber = e.EndLineNumber > 0 ? (int?)e.EndLineNumber : null;
                    int? endColumnNumber = e.EndColumnNumber > 0 ? (int?)e.EndColumnNumber : null;
                    string projectFile = e.ProjectFile;
                    string filePath = null;
                    string stack = null;
                    string subCategory = e.Subcategory;

                    projectSpan.Error = true;
                    projectSpan.SetTag(BuildTags.ErrorMessage, message);
                    projectSpan.SetTag(BuildTags.ErrorType, type);
                    projectSpan.SetTag(BuildTags.ErrorCode, code);
                    projectSpan.SetTag(BuildTags.ErrorStartLine, lineNumber.ToString());
                    projectSpan.SetTag(BuildTags.ErrorStartColumn, columnNumber.ToString());
                    projectSpan.SetTag(BuildTags.ErrorProjectFile, projectFile);

                    if (!string.IsNullOrEmpty(e.File))
                    {
                        filePath = Path.Combine(Path.GetDirectoryName(projectFile), e.File);
                        projectSpan.SetTag(BuildTags.ErrorFile, filePath);
                        if (lineNumber.HasValue && lineNumber != 0)
                        {
                            stack = $" at Source code in {filePath}:line {e.LineNumber}";
                            projectSpan.SetTag(BuildTags.ErrorStack, stack);
                        }
                    }

                    if (!string.IsNullOrEmpty(subCategory))
                    {
                        projectSpan.SetTag(BuildTags.ErrorSubCategory, subCategory);
                    }

                    if (endLineNumber.HasValue && endLineNumber != 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorEndLine, endLineNumber.ToString());
                    }

                    if (endColumnNumber.HasValue && endColumnNumber != 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorEndColumn, endColumnNumber.ToString());
                    }

                    _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Error, e.Message, projectSpan));
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

                int context = e.BuildEventContext.ProjectContextId;
                if (_projects.TryGetValue(context, out Span projectSpan))
                {
                    string correlation = $"[{CorrelationIdentifier.TraceIdKey}={projectSpan.TraceId},{CorrelationIdentifier.SpanIdKey}={projectSpan.SpanId}]";
                    string message = e.Message;
                    string type = $"{e.SenderName?.ToUpperInvariant()} ({e.Code}) Warning";
                    string code = e.Code;
                    int? lineNumber = e.LineNumber > 0 ? (int?)e.LineNumber : null;
                    int? columnNumber = e.ColumnNumber > 0 ? (int?)e.ColumnNumber : null;
                    int? endLineNumber = e.EndLineNumber > 0 ? (int?)e.EndLineNumber : null;
                    int? endColumnNumber = e.EndColumnNumber > 0 ? (int?)e.EndColumnNumber : null;
                    string projectFile = e.ProjectFile;
                    string filePath = null;
                    string stack = null;
                    string subCategory = e.Subcategory;

                    if (!string.IsNullOrEmpty(e.File))
                    {
                        filePath = Path.Combine(Path.GetDirectoryName(projectFile), e.File);
                        if (lineNumber.HasValue && lineNumber != 0)
                        {
                            stack = $" at Source code in {filePath}:line {e.LineNumber}";
                        }
                    }

                    _tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new MsBuildLogEvent(DirectSubmissionLogLevelExtensions.Warning, e.Message, projectSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in WarningRaised event");
            }
        }

        private class MsBuildLogEvent : DatadogLogEvent
        {
            private readonly string _level;
            private readonly string _message;
            private readonly Context? _context;

            public MsBuildLogEvent(string level, string message, Span span)
            {
                _level = level;
                _message = message;
                _context = span is null ? null : new Context(span.TraceId, span.SpanId, span.Context.Origin);
            }

            public override void Format(StringBuilder sb, LogFormatter formatter)
            {
                formatter.FormatLog<Context?>(
                    sb,
                    in _context,
                    DateTime.UtcNow,
                    _message,
                    eventId: null,
                    logLevel: _level,
                    exception: null,
                    (JsonTextWriter writer, in Context? state) =>
                    {
                        if (state.HasValue)
                        {
                            writer.WritePropertyName("dd.trace_id");
                            writer.WriteValue($"{state.Value.TraceId}");
                            writer.WritePropertyName("dd.span_id");
                            writer.WriteValue($"{state.Value.SpanId}");
                            writer.WritePropertyName("_dd.origin");
                            writer.WriteValue($"{state.Value.Origin}");
                        }

                        return default;
                    });
            }

            private readonly struct Context
            {
                public readonly ulong TraceId;
                public readonly ulong SpanId;
                public readonly string Origin;

                public Context(ulong traceId, ulong spanId, string origin)
                {
                    TraceId = traceId;
                    SpanId = spanId;
                    Origin = origin;
                }
            }
        }
    }
}
