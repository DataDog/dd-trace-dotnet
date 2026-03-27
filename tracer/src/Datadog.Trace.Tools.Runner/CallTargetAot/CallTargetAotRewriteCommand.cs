// <copyright file="CallTargetAotRewriteCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the internal rewrite command invoked from the generated MSBuild targets file.
/// </summary>
internal class CallTargetAotRewriteCommand : CommandWithExamples
{
    private readonly Option<string> _manifestOption = new("--manifest", "Manifest path emitted by calltarget-aot generate.")
    {
        IsRequired = true
    };

    private readonly Option<string> _applicationAssemblyOption = new("--application-assembly", "Compiled application assembly path that should be rewritten.")
    {
        IsRequired = true
    };

    private readonly Option<string?> _referenceAssemblyListOption = new("--reference-assembly-list", "Semicolon-delimited copy-local reference assembly list captured by the generated targets file.");

    private readonly Option<string> _outputDirectoryOption = new("--output-directory", "Directory that receives the rewritten assembly outputs.")
    {
        IsRequired = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotRewriteCommand"/> class.
    /// </summary>
    public CallTargetAotRewriteCommand()
        : base("rewrite", "Rewrite the compiled app assembly so its module initializer calls the milestone bootstrap")
    {
        AddOption(_manifestOption);
        AddOption(_applicationAssemblyOption);
        AddOption(_referenceAssemblyListOption);
        AddOption(_outputDirectoryOption);

        this.SetHandler(Execute);
    }

    /// <summary>
    /// Executes the rewrite command and stores its exit code in the invocation context.
    /// </summary>
    /// <param name="context">The command invocation context.</param>
    private void Execute(InvocationContext context)
    {
        context.ExitCode = CallTargetAotRewriteProcessor.Process(
            _manifestOption.GetValue(context),
            _applicationAssemblyOption.GetValue(context),
            _referenceAssemblyListOption.GetValue(context),
            _outputDirectoryOption.GetValue(context));
    }
}
