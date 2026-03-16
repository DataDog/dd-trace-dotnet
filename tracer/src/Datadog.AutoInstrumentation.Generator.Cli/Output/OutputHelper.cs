// <copyright file="OutputHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal static class OutputHelper
{
    public static int WriteSuccess(bool jsonMode, string command, object? data, string? plainText = null)
    {
        if (jsonMode)
        {
            var result = new CliResult
            {
                Success = true,
                Command = command,
                Data = data,
            };
            Console.Write(JsonOutputFormatter.FormatResult(result));
        }
        else if (plainText is not null)
        {
            Console.Write(plainText);
        }

        return 0;
    }

    public static int WriteError(bool jsonMode, string command, string errorCode, string errorMessage, object? data = null)
    {
        if (jsonMode)
        {
            var result = new CliResult
            {
                Success = false,
                Command = command,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Data = data,
            };
            Console.Write(JsonOutputFormatter.FormatResult(result));
        }
        else
        {
            Console.Error.WriteLine(errorMessage);
        }

        return 1;
    }
}
