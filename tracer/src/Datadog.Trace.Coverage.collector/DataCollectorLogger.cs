// <copyright file="DataCollectorLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Datadog.Trace.Coverage.Collector
{
    internal class DataCollectorLogger : ICollectorLogger
    {
        private readonly IDatadogLogger _datadogLogger = DatadogSerilogLogger.NullLogger;
        private readonly DataCollectionLogger _logger;
        private readonly bool _isDebugEnabled;
        private DataCollectionContext _collectionContext;

        public DataCollectorLogger(DataCollectionLogger logger, DataCollectionContext collectionContext)
        {
            _logger = logger;
            _collectionContext = collectionContext;
            _isDebugEnabled = GlobalSettings.Instance.DebugEnabledInternal;

            var source = GlobalConfigurationSource.Instance;
            var hasFileSink = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance)
                             .WithKeys(ConfigurationKeys.LogSinks)
                             .AsString()
                            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Contains(LogSinkOptions.File) ?? true;
            if (hasFileSink && DatadogLoggingFactory.GetFileLoggingConfiguration(source, NullConfigurationTelemetry.Instance) is { } fileConfig)
            {
                var loggerConfiguration = new LoggerConfiguration().Enrich.FromLogContext().MinimumLevel.Debug();

                // Ends in a dash because of the date postfix
                loggerConfiguration
                   .WriteTo.File(
                        Path.Combine(fileConfig.LogDirectory, "dotnet-tracer-managed-coverage-collector-.log"),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Exception}{NewLine}",
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: fileConfig.MaxLogFileSizeBytes,
                        shared: true);

                _datadogLogger = new DatadogSerilogLogger(loggerConfiguration.CreateLogger(), new NullLogRateLimiter(), fileConfig.LogDirectory);
            }
        }

        public bool IsDebugEnabled => _isDebugEnabled;

        public void Error(string? text)
        {
            _logger.LogWarning(_collectionContext, text ?? string.Empty);
            _datadogLogger.Error("Logged coverage tool error: {Error}", text);
        }

        public void Error(Exception exception)
        {
            _logger.LogWarning(_collectionContext, exception.ToString());
            _datadogLogger.Error(exception, "Logged coverage tool error");
        }

        public void Error(Exception exception, string? text)
        {
            var textToLogger = text;
            if (string.IsNullOrEmpty(textToLogger))
            {
                textToLogger = exception?.ToString();
            }
            else
            {
                textToLogger += Environment.NewLine + exception;
            }

            _logger.LogWarning(_collectionContext, textToLogger);
            _datadogLogger.Error(exception, "Logged coverage tool error: {Error}", text);
        }

        public void Warning(string? text)
        {
            _logger.LogWarning(_collectionContext, text ?? string.Empty);
            _datadogLogger.Warning("Logged coverage tool warning: {Warning}", text);
        }

        public void Debug(string? text)
        {
            if (_isDebugEnabled)
            {
                _datadogLogger.Debug("Logged coverage tool debug message: {Message}", text);
            }
        }

        public void SetContext(DataCollectionContext collectionContext)
        {
            _collectionContext = collectionContext;
        }
    }
}
