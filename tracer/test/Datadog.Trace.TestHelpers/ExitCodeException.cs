// <copyright file="ExitCodeException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.TestHelpers;

public class ExitCodeException : Exception
{
    private ExitCodeException(int actualExitCode, int expectedExitCode, string message)
        : base(string.IsNullOrWhiteSpace(message) ?
                   $"Expected exit code: {expectedExitCode}, actual exit code: {actualExitCode}." :
                   $"Expected exit code: {expectedExitCode}, actual exit code: {actualExitCode}. Message: {message}")
    {
    }

    public static void ThrowIfNonZero(int actualExitCode, string message = null)
        => ThrowIfNonExpected(actualExitCode, expectedExitCode: 0, message);

    public static void ThrowIfNonExpected(int actualExitCode, int expectedExitCode, string message = null)
    {
        if (actualExitCode != expectedExitCode)
        {
            Throw(actualExitCode, expectedExitCode, message);
        }
    }

    public static void Throw(int actualExitCode, int expectedExitCode, string message = null)
        => throw new ExitCodeException(actualExitCode, expectedExitCode, message);
}
