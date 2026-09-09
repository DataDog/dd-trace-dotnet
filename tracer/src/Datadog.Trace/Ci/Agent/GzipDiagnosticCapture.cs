// <copyright file="GzipDiagnosticCapture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Agent;

/// <summary>
/// Temporary, opt-in capture for comparing gzip bytes at the sender and test agent.
/// The shared directory bounds disk use across all test processes, not just one failing test.
/// </summary>
internal static class GzipDiagnosticCapture
{
    internal const string RequestHeader = "X-Datadog-Gzip-Diagnostic-Id";
    internal const int MaxBodyBytes = 1024 * 1024;
    internal const int MaxCaptures = 64;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GzipDiagnosticCapture));

    /// <summary>
    /// Snapshots each HTTP attempt before transport can touch its buffer. Successful requests leave no files.
    /// A failed request retains the original bytes and both hashes, so buffer mutation is distinguishable from bad gzip.
    /// </summary>
    internal static async Task<IApiResponse> SendAsync(IApiRequest request, ArraySegment<byte> body, string directory)
    {
        byte[]? snapshot = null;
        string? beforeHash = null;
        var requestId = Guid.NewGuid().ToString("N");
        try
        {
            snapshot = new byte[Math.Min(body.Count, MaxBodyBytes)];
            Buffer.BlockCopy(body.Array!, body.Offset, snapshot, 0, snapshot.Length);
            beforeHash = Hash(body);
            request.AddHeader(RequestHeader, requestId);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Unable to prepare gzip diagnostic capture.");
        }

        var failed = true;
        var statusCode = -1;
        try
        {
            var response = await request.PostAsync(body, MimeTypes.MsgPack, "gzip").ConfigureAwait(false);
            statusCode = response.StatusCode;
            failed = statusCode is < 200 or >= 300;
            return response;
        }
        finally
        {
            if (failed && snapshot is not null)
            {
                try
                {
                    Save(directory, new ArraySegment<byte>(snapshot), $"Side: sender\nRequest: {requestId}\nHTTP status: {statusCode}\nOriginal bytes: {body.Count}\nSnapshot truncated: {body.Count > snapshot.Length}\nSHA256 before send: {beforeHash}\nSHA256 after send: {Hash(body)}");
                }
                catch (Exception exception)
                {
                    // Capturing evidence must not replace the original transport failure.
                    Log.Warning(exception, "Unable to finish gzip diagnostic capture.");
                }
            }
        }
    }

    /// <summary>
    /// Reserves one of 64 files atomically across processes. Bodies over 1 MiB are explicitly marked as truncated.
    /// Only caller-selected framing metadata is recorded; authentication headers must never be passed here.
    /// </summary>
    internal static void Save(string directory, ArraySegment<byte> body, string metadata)
    {
        try
        {
            Directory.CreateDirectory(directory);
            for (var slot = 0; slot < MaxCaptures; slot++)
            {
                var path = Path.Combine(directory, $"capture-{slot}");
                FileStream file;
                try
                {
                    file = new FileStream(path + ".bin", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(path + ".bin"))
                {
                    continue;
                }

                using (file)
                {
                    file.Write(body.Array!, body.Offset, Math.Min(body.Count, MaxBodyBytes));
                }

                var details = $"Process: {DomainMetadata.Instance.ProcessId}\nUTC: {DateTime.UtcNow:O}\nBody bytes: {body.Count}\nCaptured bytes: {Math.Min(body.Count, MaxBodyBytes)}\nTruncated: {body.Count > MaxBodyBytes}\nSHA256: {Hash(body)}\n{metadata}";
                File.WriteAllText(path + ".txt", details.Length <= 8192 ? details : details.Substring(0, 8192));
                return;
            }

            Log.Warning("Gzip diagnostic capture limit reached; further captures will be omitted.");
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Unable to save gzip diagnostic capture.");
        }
    }

    private static string Hash(ArraySegment<byte> body)
    {
#if NET6_0_OR_GREATER
        return BitConverter.ToString(SHA256.HashData(body.AsSpan()));
#else
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(body.Array!, body.Offset, body.Count));
#endif
    }
}
