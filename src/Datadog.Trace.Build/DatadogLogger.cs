using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;
using Microsoft.Build.Framework;

namespace Datadog.Trace.Build
{
    /// <summary>
    /// Build logger
    /// </summary>
    public class DatadogLogger : INodeLogger
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(DatadogLogger));
        private static readonly bool _inContainer;

        private Tracer _tracer = null;
        private Span _buildSpan = null;
        private ConcurrentDictionary<int, Span> _projects = new ConcurrentDictionary<int, Span>();

        static DatadogLogger()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);

            _inContainer = EnvironmentHelpers.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || ContainerMetadata.GetContainerId() != null;
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

            _tracer = Tracer.Instance;

            eventSource.BuildStarted += EventSource_BuildStarted;
            eventSource.BuildFinished += EventSource_BuildFinished;
            eventSource.ProjectStarted += EventSource_ProjectStarted;
            eventSource.ProjectFinished += EventSource_ProjectFinished;
            eventSource.ErrorRaised += EventSource_ErrorRaised;
            eventSource.WarningRaised += EventSource_WarningRaised;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            _tracer.FlushAsync().GetAwaiter().GetResult();
        }

        private void EventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
            Log.Information("Build Started");

            _buildSpan = _tracer.StartSpan(BuildTags.BuildOperationName);
            _buildSpan.SetMetric(Tags.Analytics, 1.0d);
            _buildSpan.SetTraceSamplingPriority(SamplingPriority.UserKeep);

            _buildSpan.Type = SpanTypes.Build;
            _buildSpan.SetTag(BuildTags.BuildName, e.SenderName);
            foreach (KeyValuePair<string, string> envValue in e.BuildEnvironment)
            {
                _buildSpan.SetTag($"{BuildTags.BuildEnvironment}.{envValue.Key}", envValue.Value);
            }

            _buildSpan.SetTag(BuildTags.BuildCommand, Environment.CommandLine);
            _buildSpan.SetTag(BuildTags.BuildWorkingFolder, Environment.CurrentDirectory);
            _buildSpan.SetTag(BuildTags.BuildStartMessage, e.Message);

            _buildSpan.SetTag(BuildTags.BuildInContainer, _inContainer ? "true" : "false");
            _buildSpan.SetTag(BuildTags.RuntimeOSArchitecture, Environment.Is64BitOperatingSystem ? "x64" : "x86");
            _buildSpan.SetTag(BuildTags.RuntimeProcessArchitecture, Environment.Is64BitProcess ? "x64" : "x86");

            CIEnvironmentValues.DecorateSpan(_buildSpan);
        }

        private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            Log.Information("Build Finished");

            _buildSpan.SetTag(BuildTags.BuildStatus, e.Succeeded ? BuildTags.BuildSucceededStatus : BuildTags.BuildFailedStatus);
            if (!e.Succeeded)
            {
                _buildSpan.Error = true;
            }

            _buildSpan.SetTag(BuildTags.BuildEndMessage, e.Message);
            _buildSpan.Finish(e.Timestamp);
        }

        private void EventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (e.TargetNames.StartsWith("_"))
            {
                // return;
            }

            Log.Information("Project Started");

            int parentContext = e.ParentProjectBuildEventContext.ProjectContextId;
            int context = e.BuildEventContext.ProjectContextId;
            if (!_projects.TryGetValue(parentContext, out Span parentSpan))
            {
                parentSpan = _buildSpan;
            }

            string projectName = Path.GetFileName(e.ProjectFile);

            Span projectSpan = _tracer.StartSpan(BuildTags.BuildOperationName, parent: parentSpan.Context, serviceName: projectName + "-build");
            projectSpan.ResourceName = projectName;
            projectSpan.SetMetric(Tags.Analytics, 1.0d);
            projectSpan.SetTraceSamplingPriority(SamplingPriority.UserKeep);
            projectSpan.Type = SpanTypes.Build;

            foreach (KeyValuePair<string, string> prop in e.GlobalProperties)
            {
                projectSpan.SetTag($"{BuildTags.ProjectProperties}.{prop.Key}", prop.Value);
            }

            projectSpan.SetTag(BuildTags.ProjectFile, e.ProjectFile);
            projectSpan.SetTag(BuildTags.ProjectSenderName, e.SenderName);
            projectSpan.SetTag(BuildTags.ProjectTargetNames, e.TargetNames);
            projectSpan.SetTag(BuildTags.ProjectToolsVersion, e.ToolsVersion);
            projectSpan.SetTag(BuildTags.BuildName, projectName);
            projectSpan.SetTag(BuildTags.BuildStartMessage, e.Message);
            _projects.TryAdd(context, projectSpan);
        }

        private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            int context = e.BuildEventContext.ProjectContextId;
            if (_projects.TryRemove(context, out Span projectSpan))
            {
                Log.Information("Project Finished");

                projectSpan.SetTag(BuildTags.BuildStatus, e.Succeeded ? BuildTags.BuildSucceededStatus : BuildTags.BuildFailedStatus);
                if (!e.Succeeded)
                {
                    projectSpan.Error = true;
                }

                projectSpan.SetTag(BuildTags.BuildEndMessage, e.Message);
                projectSpan.Finish(e.Timestamp);
            }
        }

        private void EventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            Log.Information("Error Raised");

            int context = e.BuildEventContext.ProjectContextId;
            if (_projects.TryGetValue(context, out Span projectSpan))
            {
                projectSpan.Error = true;
                projectSpan.SetTag(BuildTags.ErrorMessage, e.Message);
                projectSpan.SetTag(BuildTags.ErrorType, e.SenderName?.ToUpperInvariant() + " Error");
                projectSpan.SetTag(BuildTags.ErrorCode, e.Code);
                projectSpan.SetTag(BuildTags.ErrorStartLine, e.LineNumber.ToString());
                projectSpan.SetTag(BuildTags.ErrorStartColumn, e.ColumnNumber.ToString());
                projectSpan.SetTag(BuildTags.ErrorProjectFile, e.ProjectFile);

                if (!string.IsNullOrEmpty(e.File))
                {
                    var filePath = Path.Combine(Path.GetDirectoryName(e.ProjectFile), e.File);

                    projectSpan.SetTag(BuildTags.ErrorFile, filePath);
                    if (e.LineNumber != 0)
                    {
                        projectSpan.SetTag(BuildTags.ErrorStack, $" at Source code in {filePath}:line {e.LineNumber}");
                    }
                }

                if (!string.IsNullOrEmpty(e.Subcategory))
                {
                    projectSpan.SetTag(BuildTags.ErrorSubCategory, e.Subcategory);
                }

                if (e.EndLineNumber != 0)
                {
                    projectSpan.SetTag(BuildTags.ErrorEndLine, e.EndLineNumber.ToString());
                }

                if (e.EndColumnNumber != 0)
                {
                    projectSpan.SetTag(BuildTags.ErrorEndColumn, e.EndColumnNumber.ToString());
                }
            }
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            Log.Information("Warning Raised");

            int context = e.BuildEventContext.ProjectContextId;
            if (_projects.TryGetValue(context, out Span projectSpan))
            {
                Span warningSpan = _tracer.StartSpan("build.warning-event", projectSpan.Context);

                warningSpan.SetTag(BuildTags.WarningMessage, e.Message);
                warningSpan.SetTag(BuildTags.WarningType, e.SenderName?.ToUpperInvariant() + " Warning");
                warningSpan.SetTag(BuildTags.WarningCode, e.Code);
                warningSpan.SetTag(BuildTags.WarningStartLine, e.LineNumber.ToString());
                warningSpan.SetTag(BuildTags.WarningStartColumn, e.ColumnNumber.ToString());
                warningSpan.SetTag(BuildTags.WarningProjectFile, e.ProjectFile);

                if (!string.IsNullOrEmpty(e.File))
                {
                    var filePath = Path.Combine(Path.GetDirectoryName(e.ProjectFile), e.File);

                    warningSpan.SetTag(BuildTags.WarningFile, filePath);
                    if (e.LineNumber != 0)
                    {
                        warningSpan.SetTag(BuildTags.WarningStack, $" at Source code in {filePath}:line {e.LineNumber}");
                    }
                }

                if (!string.IsNullOrEmpty(e.Subcategory))
                {
                    warningSpan.SetTag(BuildTags.WarningSubCategory, e.Subcategory);
                }

                if (e.EndLineNumber != 0)
                {
                    warningSpan.SetTag(BuildTags.WarningEndLine, e.EndLineNumber.ToString());
                }

                if (e.EndColumnNumber != 0)
                {
                    warningSpan.SetTag(BuildTags.WarningEndColumn, e.EndColumnNumber.ToString());
                }

                warningSpan.Finish();
            }
        }
    }
}
