// <copyright file="LoggerTextWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    internal class LoggerTextWriter : TextWriter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LoggerTextWriter>();

        private readonly bool _isDebug;
        private readonly DataCollectionContext _context;
        private readonly DataCollectionLogger _logger;

        public LoggerTextWriter(DataCollectionContext context, DataCollectionLogger logger)
        {
            _context = context;
            _logger = logger;

            var settings = GlobalSettings.FromDefaultSources();
            _isDebug = settings.DebugEnabled;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string? value)
        {
            if (_isDebug)
            {
                _logger.LogWarning(_context, value);
                Log.Warning(value);
            }
        }

        public override void WriteLine(string? value)
        {
            if (_isDebug)
            {
                _logger.LogWarning(_context, value);
                Log.Warning(value);
            }
        }
    }
}
