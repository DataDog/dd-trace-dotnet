// <copyright file="LocationInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.SourceGenerators.Helpers;

internal record LocationInfo
{
    private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    public string FilePath { get; }

    public TextSpan TextSpan { get; }

    public LinePositionSpan LineSpan { get; }

    public Location ToLocation()
        => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? CreateFrom(SyntaxNode? node)
        => CreateFrom(node?.GetLocation());

    public static LocationInfo? CreateFrom(Location? location)
    {
        if (location?.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public void Deconstruct(out string filePath, out TextSpan textSpan, out LinePositionSpan lineSpan)
    {
        filePath = FilePath;
        textSpan = TextSpan;
        lineSpan = LineSpan;
    }
}
