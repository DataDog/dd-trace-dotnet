// <copyright file="GlobalCoverageArtifactWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageArtifactWriter
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

    private readonly GlobalCoverageArtifactLimits _limits;
    private readonly GlobalCoverageInputReader _validator;

    public GlobalCoverageArtifactWriter(GlobalCoverageArtifactLimits? limits = null)
    {
        _limits = limits ?? GlobalCoverageArtifactLimits.Default;
        _validator = new GlobalCoverageInputReader(_limits);
    }

    public void WriteAtomicNoReplace(string destinationPath, GlobalCoverageInfo model)
    {
        using var staged = Stage(destinationPath, model, replaceExisting: false);
        staged.Commit();
    }

    public void WriteAtomicReplace(string destinationPath, GlobalCoverageInfo model)
    {
        using var staged = Stage(destinationPath, model, replaceExisting: true);
        staged.Commit();
    }

    public GlobalCoverageStagedArtifact StageNoReplace(string destinationPath, GlobalCoverageInfo model)
        => Stage(destinationPath, model, replaceExisting: false);

    public GlobalCoverageStagedArtifact StageReplace(string destinationPath, GlobalCoverageInfo model)
        => Stage(destinationPath, model, replaceExisting: true);

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A failed write must preserve the primary exception. The non-json temporary is never a valid input.
        }
    }

    private GlobalCoverageStagedArtifact Stage(string destinationPath, GlobalCoverageInfo model, bool replaceExisting)
    {
        if (StringUtil.IsNullOrWhiteSpace(destinationPath))
        {
            ThrowHelper.ThrowArgumentException("A global coverage destination path is required.", nameof(destinationPath));
        }

        _validator.ValidateModel(model);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestinationPath);
        if (StringUtil.IsNullOrEmpty(directory))
        {
            ThrowHelper.ThrowInvalidOperationException("The global coverage destination has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.SequentialScan))
            using (var boundedStream = new GlobalCoverageBoundedWriteStream(
                       fileStream,
                       _limits.MaximumSerializedBytes,
                       "The global coverage serialized-byte limit was exceeded."))
            using (var streamWriter = new StreamWriter(boundedStream, Utf8WithoutBom, 16 * 1024, true))
            using (var jsonWriter = new JsonTextWriter(streamWriter) { ArrayPool = JsonArrayPool.Shared })
            {
                JsonSerializer.Create().Serialize(jsonWriter, model);
                jsonWriter.Flush();
                streamWriter.Flush();
                boundedStream.Flush();
                fileStream.Flush(true);
            }

            return new GlobalCoverageStagedArtifact(temporaryPath, fullDestinationPath, replaceExisting);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }
}
