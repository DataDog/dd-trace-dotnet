using System.Security.Cryptography;
using System.Text.Json;

namespace Datadog.Trace.Tools.RemoteConfigConformance;

internal static class AssignmentFixture
{
    internal static byte[] Encode(int version, string backendState, IEnumerable<TargetFile> assignments)
    {
        var files = assignments.ToArray();
        var targets = files.ToDictionary(
            file => file.Path,
            file => new
            {
                custom = new { v = file.Revision },
                hashes = new { sha256 = Convert.ToHexString(SHA256.HashData(file.Contents)).ToLowerInvariant() },
                length = file.Contents.Length,
            },
            StringComparer.Ordinal);
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new { signed = new { custom = new { opaque_backend_state = backendState }, targets, version } });
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            targets = metadata,
            target_files = files.Where(file => file.IncludePayload).Select(file => new { path = file.Path, raw = file.Contents }),
            client_configs = files.Select(file => file.Path),
        });
    }

    internal readonly record struct TargetFile(string Path, byte[] Contents, ulong Revision, bool IncludePayload = true);
}
