// <copyright file="DuckTypeAotVerifyCompatCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal class DuckTypeAotVerifyCompatCommand : CommandWithExamples
    {
        private readonly Option<string> _compatReportOption = new("--compat-report", "Path to the generated compatibility markdown report.")
        {
            IsRequired = true
        };

        private readonly Option<string> _compatMatrixOption = new("--compat-matrix", "Path to the generated compatibility matrix JSON report.")
        {
            IsRequired = true
        };

        public DuckTypeAotVerifyCompatCommand()
            : base("verify-compat", "Validate generated compatibility artifacts for Bible parity coverage")
        {
            AddOption(_compatReportOption);
            AddOption(_compatMatrixOption);

            AddExample("dd-trace ducktype-aot verify-compat --compat-report ducktyping-aot-compat.md --compat-matrix ducktyping-aot-compat.json");

            this.SetHandler(Execute);
        }

        private void Execute(InvocationContext context)
        {
            var compatReportPath = _compatReportOption.GetValue(context);
            var compatMatrixPath = _compatMatrixOption.GetValue(context);

            var options = new DuckTypeAotVerifyCompatOptions(compatReportPath, compatMatrixPath);
            context.ExitCode = DuckTypeAotVerifyCompatProcessor.Process(options);
        }
    }
}
