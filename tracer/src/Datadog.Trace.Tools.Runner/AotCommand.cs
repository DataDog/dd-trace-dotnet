// <copyright file="AotCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner
{
    internal class AotCommand : CommandWithExamples
    {
        private readonly Argument<string> _inputFolderArgument = new("input-folder");
        private readonly Argument<string> _outputFolderArgument = new("output-folder");

        public AotCommand()
            : base("apply-aot", "Apply AOT automatic instrumentation on application folder")
        {
            AddArgument(_inputFolderArgument);
            AddArgument(_outputFolderArgument);

            AddExample(@"dd-trace apply-aot c:\input\ c:\output\");

            this.SetHandler(Execute);
        }

        private void Execute(InvocationContext context)
        {
            var inputFolder = _inputFolderArgument.GetValue(context);
            var outputFolder = _outputFolderArgument.GetValue(context);

            try
            {
                Aot.AotProcessor.ProcessFolder(inputFolder, outputFolder);
            }
            catch
            {
                context.ExitCode = 1;
            }
        }
    }
}
