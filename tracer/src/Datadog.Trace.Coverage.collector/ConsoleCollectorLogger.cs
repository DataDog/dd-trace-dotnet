// <copyright file="ConsoleCollectorLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    internal class ConsoleCollectorLogger : ICollectorLogger
    {
        public void Error(string? text)
        {
            Console.WriteLine("{0} [ERR] {1}", DateTime.Now, text);
        }

        public void Error(Exception exception)
        {
            Console.WriteLine("{0} [ERR] {1}", DateTime.Now, exception);
        }

        public void Error(Exception exception, string? text)
        {
            Console.WriteLine("{0} [ERR] {1}\r\n{2}", DateTime.Now, text, exception);
        }

        public void Warning(string? text)
        {
            Console.WriteLine("{0} [WRN] {1}", DateTime.Now, text);
        }

        public void Debug(string? text)
        {
            Console.WriteLine("{0} [DBG] {1}", DateTime.Now, text);
        }
    }
}
