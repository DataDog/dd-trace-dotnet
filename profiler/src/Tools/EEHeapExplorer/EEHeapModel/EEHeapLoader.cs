// <copyright file="EEHeapLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace EEHeapModel;

/// <summary>
/// Loads an <see cref="EEHeapReport"/> from an eeheap.json file, or from a .zip archive that
/// contains one (the first entry matching eeheap*.json, case-insensitive).
/// </summary>
public static class EEHeapLoader
{
    /// <summary>
    /// Loads an eeheap report from a .json file or a .zip containing an eeheap*.json entry.
    /// </summary>
    public static EEHeapReport LoadFromFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path must be provided.", nameof(path));
        }

        var json = IsZip(path) ? ReadJsonFromZip(path) : File.ReadAllText(path);
        return Parse(json);
    }

    /// <summary>
    /// Parses an eeheap.json document into an <see cref="EEHeapReport"/>.
    /// </summary>
    public static EEHeapReport Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var source = root.TryGetProperty("source", out var sourceElement)
            ? sourceElement.GetString() ?? "unknown"
            : "unknown";

        var regions = new List<HeapRegion>();
        if (root.TryGetProperty("heaps", out var heapsElement) && heapsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var heap in heapsElement.EnumerateArray())
            {
                regions.Add(ParseRegion(heap));
            }
        }

        return new EEHeapReport { Source = source, Heaps = regions };
    }

    private static HeapRegion ParseRegion(JsonElement heap)
    {
        return new HeapRegion
        {
            AddressHex = heap.TryGetProperty("address", out var address) ? address.GetString() ?? "0x0" : "0x0",
            Reserved = ReadUInt64(heap, "size"),
            Committed = ReadUInt64(heap, "committed"),
            Kind = heap.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "Unknown" : "Unknown",
            State = heap.TryGetProperty("state", out var state) ? state.GetString() ?? "None" : "None",
            GcHeap = ReadInt32(heap, "gc_heap", -1),
            Generation = ReadInt32(heap, "generation", -1),
        };
    }

    private static ulong ReadUInt64(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetUInt64(out var result) ? result : 0;
    }

    private static int ReadInt32(JsonElement element, string name, int fallback)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    }

    private static bool IsZip(string path)
    {
        return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadJsonFromZip(string path)
    {
        using var archive = ZipFile.OpenRead(path);

        var entry = archive.Entries.FirstOrDefault(e =>
            e.Name.StartsWith("eeheap", StringComparison.OrdinalIgnoreCase) &&
            e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            throw new InvalidDataException($"No eeheap*.json entry found in archive '{path}'.");
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
