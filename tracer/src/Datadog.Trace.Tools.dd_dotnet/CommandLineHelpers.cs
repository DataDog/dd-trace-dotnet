// <copyright file="CommandLineHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Datadog.Trace.Tools.dd_dotnet;

internal static class CommandLineHelpers
{
    public static string[] ParseArrayArgument(ArgumentResult argument)
    {
        var result = new string[argument.Tokens.Count];

        for (int i = 0; i < argument.Tokens.Count; i++)
        {
            result[i] = argument.Tokens[i].Value;
        }

        return result;
    }

    public static T? GetValue<T>(this Option<T> option, InvocationContext context)
    {
        return context.ParseResult.GetValueForOption(option);
    }

    public static T GetValue<T>(this Argument<T> argument, InvocationContext context)
    {
        return context.ParseResult.GetValueForArgument(argument);
    }
}
