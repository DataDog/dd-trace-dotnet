using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Microsoft.Build.Framework;

namespace Datadog.Trace.Build
{
    /// <summary>
    /// Build logger
    /// </summary>
    public class DatadogLogger : INodeLogger
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(DatadogLogger));

        private Tracer _tracer = null;
        private Span _buildSpan = null;
        private ConcurrentDictionary<int, Span> _projects = new ConcurrentDictionary<int, Span>();

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

            _buildSpan = _tracer.StartSpan("Build");
            _buildSpan.SetMetric(Tags.Analytics, 1.0d);
            _buildSpan.SetTraceSamplingPriority(SamplingPriority.UserKeep);

            _buildSpan.Type = "build";
            _buildSpan.SetTag("build.name", e.SenderName);
            foreach (KeyValuePair<string, string> envValue in e.BuildEnvironment)
            {
                _buildSpan.SetTag("build.environment." + envValue.Key, envValue.Value);
            }

            _buildSpan.SetTag("build.startMessage", e.Message);
        }

        private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            Log.Information("Build Finished");

            if (e.Succeeded)
            {
                _buildSpan.SetTag("build.status", "Succeeded");
            }
            else
            {
                _buildSpan.SetTag("build.status", "Failed");
                _buildSpan.Error = true;
            }

            _buildSpan.SetTag("build.finishMessage", e.Message);
            _buildSpan.Finish(e.Timestamp);
        }

        private void EventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (e.TargetNames.StartsWith("_"))
            {
                return;
            }

            Log.Information("Project Started");

            int parentContext = e.ParentProjectBuildEventContext.ProjectContextId;
            int context = e.BuildEventContext.ProjectContextId;
            if (!_projects.TryGetValue(parentContext, out Span parentSpan))
            {
                parentSpan = _buildSpan;
            }

            string projectName = Path.GetFileName(e.ProjectFile);
            string operationName = string.IsNullOrEmpty(e.TargetNames) ?
                $"{projectName}" :
                $"{projectName} [{e.TargetNames}]";

            Span projectSpan = _tracer.StartSpan(operationName, parent: parentSpan.Context);

            foreach (KeyValuePair<string, string> envValue in e.GlobalProperties)
            {
                projectSpan.SetTag("project.properties." + envValue.Key, envValue.Value);
            }

            projectSpan.SetTag("project.name", projectName);
            projectSpan.SetTag("project.file", e.ProjectFile);
            projectSpan.SetTag("project.senderName", e.SenderName);
            projectSpan.SetTag("project.targetNames", e.TargetNames);
            projectSpan.SetTag("project.toolsVersion", e.ToolsVersion);
            projectSpan.SetTag("build.startMessage", e.Message);

            _projects.TryAdd(context, projectSpan);
        }

        private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            int context = e.BuildEventContext.ProjectContextId;
            if (_projects.TryRemove(context, out Span projectSpan))
            {
                Log.Information("Project Finished");

                if (e.Succeeded)
                {
                    projectSpan.SetTag("build.status", "Succeeded");
                }
                else
                {
                    projectSpan.SetTag("build.status", "Failed");
                    projectSpan.Error = true;
                }

                projectSpan.SetTag("build.finishMessage", e.Message);

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
                projectSpan.SetTag(Trace.Tags.ErrorMsg, e.Message);
                projectSpan.SetTag(Trace.Tags.ErrorType, e.SenderName?.ToUpperInvariant() + " Error");
                projectSpan.SetTag("error.code", e.Code);
                projectSpan.SetTag("error.startLocation.line", e.LineNumber.ToString());
                projectSpan.SetTag("error.startLocation.column", e.ColumnNumber.ToString());
                projectSpan.SetTag("error.projectFile", e.ProjectFile);

                if (!string.IsNullOrEmpty(e.File))
                {
                    var filePath = Path.Combine(Path.GetDirectoryName(e.ProjectFile), e.File);

                    projectSpan.SetTag("error.file", filePath);
                    if (e.LineNumber != 0)
                    {
                        projectSpan.SetTag(Trace.Tags.ErrorStack, $" at Source code in {filePath}:line {e.LineNumber}");
                    }
                }

                if (!string.IsNullOrEmpty(e.Subcategory))
                {
                    projectSpan.SetTag("error.subCategory", e.Subcategory);
                }

                if (e.EndColumnNumber != 0)
                {
                    projectSpan.SetTag("error.endLocation.column", e.EndColumnNumber.ToString());
                }

                if (e.EndLineNumber != 0)
                {
                    projectSpan.SetTag("error.endLocation.line", e.EndLineNumber.ToString());
                }
            }
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            Log.Information("Warning Raised");

            int context = e.BuildEventContext.ProjectContextId;
            if (_projects.TryGetValue(context, out Span projectSpan))
            {
                projectSpan.SetTag("warning.msg", e.Message);
                projectSpan.SetTag("warning.type", e.SenderName?.ToUpperInvariant() + " Warning");
                projectSpan.SetTag("warning.code", e.Code);
                projectSpan.SetTag("warning.startLocation.line", e.LineNumber.ToString());
                projectSpan.SetTag("warning.startLocation.column", e.ColumnNumber.ToString());
                projectSpan.SetTag("warning.projectFile", e.ProjectFile);

                if (!string.IsNullOrEmpty(e.File))
                {
                    var filePath = Path.Combine(Path.GetDirectoryName(e.ProjectFile), e.File);

                    projectSpan.SetTag("warning.file", filePath);
                    if (e.LineNumber != 0)
                    {
                        projectSpan.SetTag("warning.stack", $" at Source code in {filePath}:line {e.LineNumber}");
                    }
                }

                if (!string.IsNullOrEmpty(e.Subcategory))
                {
                    projectSpan.SetTag("warning.subCategory", e.Subcategory);
                }

                if (e.EndColumnNumber != 0)
                {
                    projectSpan.SetTag("warning.endLocation.column", e.EndColumnNumber.ToString());
                }

                if (e.EndLineNumber != 0)
                {
                    projectSpan.SetTag("warning.endLocation.line", e.EndLineNumber.ToString());
                }
            }
        }
    }
}
