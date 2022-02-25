// <copyright file="CoverageCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    /// <summary>
    /// Datadog coverage collector
    /// </summary>
    [DataCollectorTypeUri("datacollector://Datadog/CoverageCollector/1.0")]
    [DataCollectorFriendlyName("DatadogCoverage")]
    public class CoverageCollector : DataCollector
    {
        private DataCollectionEvents? _events;
        private DataCollectionLogger? _logger;
        private XmlElement? _configurationElement;
        private DataCollectionSink? _dataSink;
        private DataCollectionContext? _dataCollectionContext;
        private CoverageProcessor? _processor;

        /// <inheritdoc />
        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            _configurationElement = configurationElement;
            _events = events;
            _dataSink = dataSink;
            _logger = logger;
            _dataCollectionContext = environmentContext.SessionDataCollectionContext;

            Console.SetOut(new LoggerTextWriter(_dataCollectionContext, _logger));
        }

        private void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            _processor = new CoverageProcessor(Environment.CurrentDirectory);
            try
            {
                _processor.Process();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(_dataCollectionContext, ex.ToString());
            }

            _logger?.LogWarning(_dataCollectionContext, "Initializing tests");
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
        }

        private void OnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
        }

        private void OnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
        }

        private void OnTestHostLaunched(object sender, TestHostLaunchedEventArgs e)
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_events != null)
            {
                _events.SessionStart -= OnSessionStart;
                _events.SessionEnd -= OnSessionEnd;
                _events.TestCaseStart -= OnTestCaseStart;
                _events.TestCaseEnd -= OnTestCaseEnd;
                _events.TestHostLaunched -= OnTestHostLaunched;
            }

            if (_processor != null)
            {
                // _processor.Dispose();
            }

            _events = null;
            _dataSink = null;
            base.Dispose(disposing);
        }
    }
}
