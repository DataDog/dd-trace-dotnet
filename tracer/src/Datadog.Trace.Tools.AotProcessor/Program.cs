// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.AotProcessor.Runtime;

namespace Datadog.Trace.Tools.AotProcessor;

internal class Program
{
    public static void Main(string[] args)
    {
        using (var rewriter = new Rewriter())
        {
            if (!rewriter.Init())
            {
                Console.WriteLine("Failed to initialize rewriter");
            }

            var path = Path.GetFullPath("C:\\_DD\\Git\\dd-trace-dotnet\\tracer\\test\\test-applications\\aot\\Samples.Aot\\bin\\Debug\\net6.0\\Samples.Aot.dll");

            rewriter.SetOutput("output");

            rewriter.ProcessAssembly(path);
        }
    }
}
