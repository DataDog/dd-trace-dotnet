// <copyright file="DuckTypeAotCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Runner.DuckTypeAot;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Represents duck type aot command.
/// </summary>
internal class DuckTypeAotCommand : CommandWithExamples
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuckTypeAotCommand"/> class.
    /// </summary>
    public DuckTypeAotCommand()
        : base("ducktype-aot", "Generate and verify NativeAOT DuckTyping registries")
    {
        AddExample("dd-trace ducktype-aot generate --proxy-assembly My.Proxy.dll --target-assembly ThirdParty.dll --output Datadog.Trace.DuckType.AotRegistry.dll");
        AddExample("dd-trace ducktype-aot verify-compat --compat-report ducktyping-aot-compat.md --compat-matrix ducktyping-aot-compat.json");

        AddCommand(new DuckTypeAotGenerateCommand());
        AddCommand(new DuckTypeAotVerifyCompatCommand());
    }
}
