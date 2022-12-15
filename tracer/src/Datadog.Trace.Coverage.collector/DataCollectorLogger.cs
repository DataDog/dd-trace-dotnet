// <copyright file="DataCollectorLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
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
            _isDebugEnabled = GlobalSettings.Instance.DebugEnabled;

            if (DatadogLoggingFactory.GetConfiguration(GlobalConfigurationSource.Instance).File is { } fileConfig)
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

                _datadogLogger = new DatadogSerilogLogger(loggerConfiguration.CreateLogger(), new NullLogRateLimiter());
            }
        }

        public void Error(string? text)
        {
            _logger.LogError(_collectionContext, text ?? string.Empty);
            _datadogLogger.Error(text);
        }

        public void Error(Exception exception)
        {
            _logger.LogError(_collectionContext, exception);
            _datadogLogger.Error(exception, exception.Message);
        }

        public void Error(Exception exception, string? text)
        {
            _logger.LogError(_collectionContext, text ?? string.Empty, exception);
            _datadogLogger.Error(exception, text);
        }

        public void Warning(string? text)
        {
            _logger.LogWarning(_collectionContext, text ?? string.Empty);
            _datadogLogger.Warning(text);
        }

        public void Debug(string? text)
        {
            if (_isDebugEnabled)
            {
                _logger.LogWarning(_collectionContext, text ?? string.Empty);
                _datadogLogger.Debug(text);
            }
        }

        public void SetContext(DataCollectionContext collectionContext)
        {
            _collectionContext = collectionContext;
        }
    }
}
