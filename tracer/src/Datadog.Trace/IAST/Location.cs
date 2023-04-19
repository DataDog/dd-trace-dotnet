// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    public Location(string? stackFile, int? line, ulong? spanId)
    {
        GetMethodPathFromStackFile(stackFile, out string? method, out string? path);
        this.Method = method;
        this.Path = path;
        this.Line = line;
        this.SpanId = spanId == 0 ? null : spanId;
    }

    public ulong? SpanId { get; }

    public string? Path { get; }

    public string? Method { get; }

    public int? Line { get; }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Path, Line, SpanId);
    }

    private void GetMethodPathFromStackFile(string? stackFile, out string? methodValue, out string? pathValue)
    {
        try
        {
            if (!string.IsNullOrEmpty(stackFile))
            {
                if (stackFile!.Contains("::") == true)
                {
                    methodValue = stackFile;
                    pathValue = null;
                }
                else
                {
                    pathValue = System.IO.Path.GetFileName(stackFile);
                    methodValue = null;
                }
            }
        }
        catch
        {
            // we have an invalid stackFile
        }

        methodValue = null;
        pathValue = null;
    }
}
