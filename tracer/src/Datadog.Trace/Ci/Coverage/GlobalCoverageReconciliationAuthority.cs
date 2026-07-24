// <copyright file="GlobalCoverageReconciliationAuthority.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageReconciliationAuthority : IDisposable
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);
    private FileStream? _claimStream;

    public GlobalCoverageReconciliationAuthority(string claimPath, FileStream claimStream)
    {
        ClaimPath = claimPath;
        _claimStream = claimStream;
    }

    public string ClaimPath { get; }

    public FileStream ClaimStream
        => Volatile.Read(ref _claimStream) ?? throw new ObjectDisposedException(nameof(GlobalCoverageReconciliationAuthority));

    public static GlobalCoverageReconciliationAuthority? TryCreate(string directory, string runToken, string kind)
    {
        var claimPath = Path.Combine(directory, GlobalCoverageProtocol.GetCommandOwnerClaimFileName(runToken));
        FileStream? claimStream = null;
        var claimCreated = false;
        try
        {
            try
            {
                claimStream = new FileStream(claimPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (File.Exists(claimPath))
            {
                return null;
            }

            claimCreated = true;
            using (var writer = new StreamWriter(claimStream, Utf8WithoutBom, 1024, true))
            {
                writer.Write("{\"version\":1,\"runToken\":\"");
                writer.Write(runToken);
                writer.Write("\",\"pid\":");
                writer.Write(DomainMetadata.Instance.ProcessId.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"kind\":\"");
                writer.Write(kind);
                writer.Write("\"}");
                writer.Flush();
                claimStream.Flush(true);
            }

            var authority = new GlobalCoverageReconciliationAuthority(claimPath, claimStream);
            claimStream = null;
            return authority;
        }
        catch
        {
            claimStream?.Dispose();
            claimStream = null;
            if (claimCreated)
            {
                TryDelete(claimPath);
            }

            throw;
        }
        finally
        {
            claimStream?.Dispose();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Preserve the claim-creation failure. A partial claim remains a durable blocker.
        }
    }

    public void Complete()
    {
        ReleaseForArchival();
        File.Delete(ClaimPath);
    }

    public string ReleaseForArchival()
    {
        var stream = Interlocked.Exchange(ref _claimStream, null) ?? throw new ObjectDisposedException(nameof(GlobalCoverageReconciliationAuthority));
        stream.Dispose();
        return ClaimPath;
    }

    public void Dispose()
        => Interlocked.Exchange(ref _claimStream, null)?.Dispose();
}
