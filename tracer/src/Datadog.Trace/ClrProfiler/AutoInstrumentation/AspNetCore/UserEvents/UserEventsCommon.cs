// <copyright file="UserEventsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

internal static class UserEventsCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UserEventsCommon));

    internal static string? GetId(IIdentityUser? user) => user?.Id?.ToString();

    internal static string? GetLogin(IIdentityUser? user) => user?.UserName ?? user?.Email;

    internal static unsafe string Anonymize(string id)
    {
        // spec says take first half of the hash
        const int bytesToUse = 16;
        #if NET6_0_OR_GREATER
        Span<byte> destination = stackalloc byte[32];
        var destinationBytes = new Span<byte>();
        var source = new ReadOnlySpan<char>(id.ToCharArray());
        var utf8 = new UTF8Encoding();
        utf8.GetBytes(source, destinationBytes);
        var successfullyHashed = SHA256.TryHashData(destinationBytes, destination, out var bytesWritten);
        #else
        var encodedBytes = Encoding.UTF8.GetBytes(id);
        using var hash = SHA256.Create();
        var destination = hash.ComputeHash(encodedBytes);
        var bytesWritten = destination.Length;
        var successfullyHashed = bytesWritten > bytesToUse;
        #endif
        if (successfullyHashed)
        {
            // we want to end up with a string of the form anon_0c76692372ebf01a7da6e9570fb7d0a1
            // 37 is 5 prefix character plus 32 hexadecimal digits (presenting 16 bytes)
            // stackalloc to avoid intermediate heap allocation
            var stringChars = stackalloc char[37];
            stringChars[0] = 'a';
            stringChars[1] = 'n';
            stringChars[2] = 'o';
            stringChars[3] = 'n';
            stringChars[4] = '_';
            for (var iBytes = 0; iBytes < bytesToUse; iBytes++)
            {
                var b = destination[iBytes];
                var iChars = iBytes * 2;
                stringChars[iChars + 5] = ByteDigitToChar(b >> 4);
                stringChars[iChars + 6] = ByteDigitToChar(b & 0x0F);
            }

            return new string(stringChars, 0, 37);
        }

        Log.Debug<int>("Couldn't anonymize user information (login or id), byteArray length was {BytesWritten}", bytesWritten);
        return string.Empty;
    }

    internal static void RecordMetricsLoginSuccessIfNotFound(bool foundUserId, bool foundLogin)
    {
        if (!foundUserId)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserId(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentityLoginSuccess);
        }

        if (!foundLogin)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserLogin(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentityLoginSuccess);
        }
    }

    internal static void RecordMetricsLoginFailureIfNotFound(bool foundUserId, bool foundLogin)
    {
        if (!foundUserId)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserId(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentityLoginFailure);
        }

        if (!foundLogin)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserLogin(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentityLoginFailure);
        }
    }

    internal static void RecordMetricsSignupIfNotFound(bool foundUserId, bool foundLogin)
    {
        if (!foundUserId)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserId(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentitySignup);
        }

        if (!foundLogin)
        {
            TelemetryFactory.Metrics.RecordCountMissingUserLogin(MetricTags.AuthenticationFrameworkWithEventType.AspNetCoreIdentitySignup);
        }
    }

    // assumes byteDigit < 15
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ByteDigitToChar(int byteDigit)
    {
        if (byteDigit <= 9)
        {
            return (char)(byteDigit + 0x30);
        }

        return (char)(byteDigit - 0x0a + 0x61);
    }
}
