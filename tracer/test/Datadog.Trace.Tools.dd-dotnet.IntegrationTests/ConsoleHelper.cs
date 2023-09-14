// <copyright file="ConsoleHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions.Execution;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests;

internal class ConsoleHelper : IDisposable
{
    private readonly IAnsiConsole _originalConsole;
    private readonly TextWriter _originalTextWriter;
    private readonly StringBuilder _output;
    private readonly AssertionScope _assertionScope;

    private ConsoleHelper()
    {
        _output = new StringBuilder();

        _originalConsole = AnsiConsole.Console;
        _originalTextWriter = Console.Out;

        _assertionScope = new AssertionScope();
        _assertionScope.AddReportable("output", () => _output.ToString());

        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new RedirectedOutput(_output) });
        Console.SetOut(new StringWriter(_output));
        Console.SetError(new StringWriter(_output));
    }

    public string Output => _output.ToString();

    public static ConsoleHelper Redirect() => new();

    public IEnumerable<string> ReadLines() => Output.Split(Environment.NewLine);

    public void Dispose()
    {
        Console.SetOut(_originalTextWriter);
        AnsiConsole.Console = _originalConsole;
        _assertionScope.Dispose();
    }

    private class RedirectedOutput : IAnsiConsoleOutput
    {
        public RedirectedOutput(StringBuilder stringBuilder)
        {
            Writer = new StringWriter(stringBuilder);
        }

        public TextWriter Writer { get; }

        public bool IsTerminal => false;

        public int Width => 640;

        public int Height => 480;

        public void SetEncoding(Encoding encoding)
        {
        }
    }
}
