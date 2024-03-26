// <copyright file="DirectSubmissionLoggerProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    /// <summary>
    /// Duck type for ILoggerProvider
    /// </summary>
    [Microsoft.Extensions.Logging.ProviderAlias("Datadog")]
    internal class DirectSubmissionLoggerProvider
    {
        private readonly Func<string, DirectSubmissionLogger> _createLoggerFunc;
        private readonly ConcurrentDictionary<string, DirectSubmissionLogger> _loggers = new();
        private readonly IDirectSubmissionLogSink _sink;
        private readonly LogFormatter? _formatter;
        private readonly DirectSubmissionLogLevel _minimumLogLevel;
        private IExternalScopeProvider? _scopeProvider;

        internal DirectSubmissionLoggerProvider(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel minimumLogLevel, IExternalScopeProvider? scopeProvider)
            : this(sink, formatter: null, minimumLogLevel, scopeProvider)
        {
        }

        // used for testing
        internal DirectSubmissionLoggerProvider(
            IDirectSubmissionLogSink sink,
            LogFormatter? formatter,
            DirectSubmissionLogLevel minimumLogLevel,
            IExternalScopeProvider? scopeProvider)
        {
            _sink = sink;
            _formatter = formatter;
            _minimumLogLevel = minimumLogLevel;
            _createLoggerFunc = CreateLoggerImplementation;
            _scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Creates a new <see cref="ILogger"/> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the logger.</param>
        /// <returns>The instance of <see cref="ILogger"/> that was created.</returns>
        [DuckReverseMethod]
        public DirectSubmissionLogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, _createLoggerFunc);
        }

        private DirectSubmissionLogger CreateLoggerImplementation(string name)
        {
            return new DirectSubmissionLogger(name, _scopeProvider, _sink, _formatter, _minimumLogLevel);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        [DuckReverseMethod]
        public void Dispose()
        {
        }

        /// <summary>
        /// Method for ISupportExternalScope
        /// </summary>
        /// <param name="scopeProvider">The provider of scope data</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Microsoft.Extensions.Logging.IExternalScopeProvider, Microsoft.Extensions.Logging.Abstractions" })]
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }
    }
}
