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

        private readonly Option<string?> _mappingCatalogOption = new("--mapping-catalog", "Optional declared mapping inventory used to enforce required mapping/scenario coverage.");
        private readonly Option<string?> _manifestOption = new("--manifest", "Optional generated manifest file used to validate matrix/manifest consistency.");
        private readonly Option<string?> _scenarioInventoryOption = new("--scenario-inventory", "Optional Bible scenario inventory contract used to enforce required scenario coverage.");
        private readonly Option<string?> _expectedOutcomesOption = new("--expected-outcomes", "Optional scenario expected-outcomes contract. When provided, each mapping status must match the expected status for its scenario (or the default status).");
        private readonly Option<string?> _knownLimitationsOption = new("--known-limitations", "Legacy alias for --expected-outcomes using known-limitations schema. Prefer --expected-outcomes.");
        private readonly Option<string> _failureModeOption = new("--failure-mode", () => "default", "Failure mode policy: 'default' warns on manifest fingerprint drift, 'strict' fails.");
        private readonly Option<bool> _strictAssemblyFingerprintsOption = new("--strict-assembly-fingerprints", "Treat manifest assembly fingerprint drift as an error instead of a warning.");

        public DuckTypeAotVerifyCompatCommand()
            : base("verify-compat", "Validate generated compatibility artifacts for Bible parity coverage")
        {
            AddOption(_compatReportOption);
            AddOption(_compatMatrixOption);
            AddOption(_mappingCatalogOption);
            AddOption(_manifestOption);
            AddOption(_scenarioInventoryOption);
            AddOption(_expectedOutcomesOption);
            AddOption(_knownLimitationsOption);
            AddOption(_failureModeOption);
            AddOption(_strictAssemblyFingerprintsOption);

            AddExample("dd-trace ducktype-aot verify-compat --compat-report ducktyping-aot-compat.md --compat-matrix ducktyping-aot-compat.json --mapping-catalog ducktyping-aot-catalog.json --scenario-inventory tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-scenario-inventory.json --expected-outcomes tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-expected-outcomes.json --manifest Datadog.Trace.DuckType.AotRegistry.dll.manifest.json --failure-mode strict");

            this.SetHandler(Execute);
        }

        private static bool TryParseFailureMode(string? value, out DuckTypeAotFailureMode failureMode)
        {
            if (string.Equals(value, "default", System.StringComparison.OrdinalIgnoreCase))
            {
                failureMode = DuckTypeAotFailureMode.Default;
                return true;
            }

            if (string.Equals(value, "strict", System.StringComparison.OrdinalIgnoreCase))
            {
                failureMode = DuckTypeAotFailureMode.Strict;
                return true;
            }

            failureMode = DuckTypeAotFailureMode.Default;
            return false;
        }

        private void Execute(InvocationContext context)
        {
            var compatReportPath = _compatReportOption.GetValue(context);
            var compatMatrixPath = _compatMatrixOption.GetValue(context);
            var mappingCatalogPath = _mappingCatalogOption.GetValue(context);
            var manifestPath = _manifestOption.GetValue(context);
            var scenarioInventoryPath = _scenarioInventoryOption.GetValue(context);
            var expectedOutcomesPath = _expectedOutcomesOption.GetValue(context);
            var knownLimitationsPath = _knownLimitationsOption.GetValue(context);
            var strictAssemblyFingerprints = _strictAssemblyFingerprintsOption.GetValue(context);
            var failureModeValue = _failureModeOption.GetValue(context);
            if (!TryParseFailureMode(failureModeValue, out var failureMode))
            {
                Datadog.Trace.Tools.Runner.Utils.WriteError($"Invalid --failure-mode value '{failureModeValue}'. Allowed values are: default, strict.");
                context.ExitCode = 1;
                return;
            }

            if (strictAssemblyFingerprints)
            {
                // Legacy flag implies strict mode for parity with previous behavior.
                failureMode = DuckTypeAotFailureMode.Strict;
            }

            var options = new DuckTypeAotVerifyCompatOptions(
                compatReportPath,
                compatMatrixPath,
                mappingCatalogPath,
                manifestPath,
                scenarioInventoryPath,
                expectedOutcomesPath,
                knownLimitationsPath,
                strictAssemblyFingerprints,
                failureMode);
            context.ExitCode = DuckTypeAotVerifyCompatProcessor.Process(options);
        }
    }
}
