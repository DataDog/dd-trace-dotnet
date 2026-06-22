// <copyright file="ReferenceTreeBinaryLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Text;

namespace ReferenceChainModel;

/// <summary>
/// Loads a <see cref="ReferenceTree"/> from the binary varint DFS format.
/// Wire format documented in docs/reference-tree-serialization-formats.md.
/// Uses <see cref="BinaryReader.Read7BitEncodedInt"/> for varint decoding.
/// </summary>
public static class ReferenceTreeBinaryLoader
{
    private static readonly byte[] Magic = "DDRT"u8.ToArray();

    // Maps RootCategory ordinal (0-8) to single-letter category code used by the model.
    private static readonly string[] CategoryCodes = ["K", "S", "F", "H", "P", "W", "R", "O", "?"];

    public static ReferenceTree LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static ReferenceTree Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadBytes(4);
        if (magic.Length < 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
        {
            throw new InvalidDataException("Invalid binary reference tree: missing DDRT magic bytes.");
        }

        int version = reader.Read7BitEncodedInt();
        int typeCount = reader.Read7BitEncodedInt();
        int rootCount = reader.Read7BitEncodedInt();

        var typeTable = new string[typeCount];
        for (int i = 0; i < typeCount; i++)
        {
            int nameLen = reader.Read7BitEncodedInt();
            var nameBytes = reader.ReadBytes(nameLen);
            typeTable[i] = Encoding.UTF8.GetString(nameBytes);
        }

        var roots = new List<ReferenceRootNode>(rootCount);
        for (int i = 0; i < rootCount; i++)
        {
            roots.Add(ReadRootNode(reader));
        }

        return new ReferenceTree(version, typeTable, roots);
    }

    private static ReferenceRootNode ReadRootNode(BinaryReader reader)
    {
        int typeIndex = reader.Read7BitEncodedInt();
        byte categoryByte = reader.ReadByte();
        long instanceCount = reader.Read7BitEncodedInt64();
        long totalSize = reader.Read7BitEncodedInt64();

        int fieldLen = reader.Read7BitEncodedInt();
        string? fieldName = fieldLen > 0
            ? Encoding.UTF8.GetString(reader.ReadBytes(fieldLen))
            : null;

        int childCount = reader.Read7BitEncodedInt();
        var children = ReadChildren(reader, childCount);

        string categoryCode = categoryByte < CategoryCodes.Length ? CategoryCodes[categoryByte] : "?";

        return new ReferenceRootNode(typeIndex, instanceCount, totalSize, categoryCode, fieldName, children);
    }

    private static ReferenceNode ReadNode(BinaryReader reader)
    {
        int typeIndex = reader.Read7BitEncodedInt();
        long instanceCount = reader.Read7BitEncodedInt64();
        long totalSize = reader.Read7BitEncodedInt64();

        int childCount = reader.Read7BitEncodedInt();
        var children = ReadChildren(reader, childCount);

        return new ReferenceNode(typeIndex, instanceCount, totalSize, children);
    }

    private static IReadOnlyList<ReferenceNode> ReadChildren(BinaryReader reader, int count)
    {
        if (count == 0)
        {
            return Array.Empty<ReferenceNode>();
        }

        var children = new List<ReferenceNode>(count);
        for (int i = 0; i < count; i++)
        {
            children.Add(ReadNode(reader));
        }

        return children;
    }
}
