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

    private static void Throw(int actualExitCode, int expectedExitCode, string message = null)
    {
        // If we are in a non windows operating system we can interpret the exit code values according the unix signals
        // to increase the detail of the cause of the exception.
        throw (actualExitCode, FrameworkDescription.Instance.IsWindows()) switch
        {
            (130, false) => new SIGINTExitCodeException(expectedExitCode, message),
            (134, false) => new SIGABRTExitCodeException(expectedExitCode, message),
            (135, false) => new SIGBUSExitCodeException(expectedExitCode, message),
            (136, false) => new SIGFPEExitCodeException(expectedExitCode, message),
            (137, false) => new SIGKILLExitCodeException(expectedExitCode, message),
            (139, false) => new SIGSEGVExitCodeException(expectedExitCode, message),
            (143, false) => new SIGTERMExitCodeException(expectedExitCode, message),
            (147, false) => new SIGSTOPExitCodeException(expectedExitCode, message),
            _ => new ExitCodeException(actualExitCode, expectedExitCode, message)
        };
    }

    private class SIGINTExitCodeException : ExitCodeException
    {
        internal SIGINTExitCodeException(int expectedExitCode, string message)
            : base(130, expectedExitCode, message)
        {
        }
    }

    private class SIGABRTExitCodeException : ExitCodeException
    {
        internal SIGABRTExitCodeException(int expectedExitCode, string message)
            : base(134, expectedExitCode, message)
        {
        }
    }

    private class SIGBUSExitCodeException : ExitCodeException
    {
        internal SIGBUSExitCodeException(int expectedExitCode, string message)
            : base(135, expectedExitCode, message)
        {
        }
    }

    private class SIGFPEExitCodeException : ExitCodeException
    {
        internal SIGFPEExitCodeException(int expectedExitCode, string message)
            : base(136, expectedExitCode, message)
        {
        }
    }

    private class SIGKILLExitCodeException : ExitCodeException
    {
        internal SIGKILLExitCodeException(int expectedExitCode, string message)
            : base(137, expectedExitCode, message)
        {
        }
    }

    private class SIGSEGVExitCodeException : ExitCodeException
    {
        internal SIGSEGVExitCodeException(int expectedExitCode, string message)
            : base(139, expectedExitCode, message)
        {
        }
    }

    private class SIGTERMExitCodeException : ExitCodeException
    {
        internal SIGTERMExitCodeException(int expectedExitCode, string message)
            : base(143, expectedExitCode, message)
        {
        }
    }

    private class SIGSTOPExitCodeException : ExitCodeException
    {
        internal SIGSTOPExitCodeException(int expectedExitCode, string message)
            : base(147, expectedExitCode, message)
        {
        }
    }
}
