// <copyright file="RaspShellInjectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

#nullable enable
namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspShellInjectionHelper
{
    internal static string BuildCommandInjectionCommand(string file, string argumentLine, Collection<string>? argumentList)
    {
        if (string.IsNullOrEmpty(file))
        {
            return string.Empty;
        }

        if ((argumentList is not null) && (argumentList.Count > 0))
        {
            return file + " " + string.Join(" ", argumentList);
        }

        if (!string.IsNullOrEmpty(argumentLine))
        {
            return file + " " + argumentLine;
        }
        else
        {
            return file;
        }
    }
}
