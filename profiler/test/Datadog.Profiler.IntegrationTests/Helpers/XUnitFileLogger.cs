// <copyright file="XUnitFileLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    // This class wraps an xunit logger and save each line into a file corresponding to the current test
    internal class XUnitFileLogger : ITestOutputHelper
    {
        private readonly string _filePath;
        private readonly ITestOutputHelper _output;

        public XUnitFileLogger(ITestOutputHelper output, string filePath)
        {
            _output = output;
            _filePath = filePath;
        }

        // ITestOuputHelper interface implementation
        public void WriteLine(string message)
        {
            _output.WriteLine(message);
            WriteLineToFile(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            var message = string.Format(format, args);
            _output.WriteLine(message);
            WriteLineToFile(message);
        }

        private void WriteLineToFile(string message)
        {
            // add the timestamp to allow correlation with other files such as profiler logs
            string timestamp = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff] ");
            try
            {
                using (var writer = new StreamWriter(_filePath, true))
                {
                    writer.WriteLine(string.Concat(timestamp, message));
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
