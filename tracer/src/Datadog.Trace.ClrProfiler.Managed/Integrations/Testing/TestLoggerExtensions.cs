// <copyright file="TestLoggerExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Test logger extensions
    /// </summary>
    internal static class TestLoggerExtensions
    {
        public static void TestMethodNotFound(this IDatadogLogger logger, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
        {
            // ReSharper disable twice ExplicitCallerInfoArgument
            logger.Error("Error: the test method can't be retrieved.", sourceLine, sourceFile);
        }

        public static void TestClassTypeNotFound(this IDatadogLogger logger, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
        {
            // ReSharper disable twice ExplicitCallerInfoArgument
            logger.Error("Error: the test class type can't be retrieved.", sourceLine, sourceFile);
        }
    }
}
