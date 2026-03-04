// <copyright file="ReferenceTree.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Represents a parsed reference tree from the JSON file.
/// The type table stores each type name exactly once; nodes reference types by index.
/// </summary>
public class ReferenceTree
{
    public ReferenceTree(int version, IReadOnlyList<string> typeTable, IReadOnlyList<ReferenceRootNode> roots)
    {
        Version = version;
        TypeTable = typeTable;
        Roots = roots;
    }

    public int Version { get; }

    /// <summary>
    /// String interning table parsed from the "tt" JSON array.
    /// Each type name is stored exactly once. Nodes reference types by index into this list.
    /// </summary>
    public IReadOnlyList<string> TypeTable { get; }

    public IReadOnlyList<ReferenceRootNode> Roots { get; }

    /// <summary>
    /// Gets the short type name from a full type name string.
    /// </summary>
    public static string GetShortTypeNameFromFullName(string fullName)
    {
        int genericStart = FindGenericStart(fullName);
        string searchRange = genericStart >= 0 ? fullName[..genericStart] : fullName;
        int lastDot = FindLastNamespaceDot(searchRange);
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }

    /// <summary>
    /// Convenience accessor to resolve a type index to its name.
    /// </summary>
    public string GetTypeName(int typeIndex)
    {
        if (typeIndex >= 0 && typeIndex < TypeTable.Count)
        {
            return TypeTable[typeIndex];
        }

        return "?";
    }

    /// <summary>
    /// Get the short name (after the last '.' in the outer type, ignoring dots inside generic parameters).
    /// Handles: Task&lt;System.Boolean&gt; → Task&lt;System.Boolean&gt;,
    /// namespace.type.&lt;&gt;c → type.&lt;&gt;c (compiler-generated).
    /// </summary>
    public string GetShortTypeName(int typeIndex)
    {
        return GetShortTypeNameFromFullName(GetTypeName(typeIndex));
    }

    private static int FindGenericStart(string fullName)
    {
        for (int i = 0; i < fullName.Length; i++)
        {
            if (fullName[i] == '<' && (i == 0 || fullName[i - 1] != '.'))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastNamespaceDot(string text)
    {
        int lastDot = -1;
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '.' && (i + 2 >= text.Length || text[i + 1] != '<' || text[i + 2] != '>'))
            {
                lastDot = i;
            }
        }

        return lastDot;
    }
}
