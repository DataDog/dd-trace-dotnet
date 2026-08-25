using System;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

sealed class PipelinePathFilter
{
    private const string MatchAllPattern = "**";

    private readonly Matcher _excludedPaths;
    private readonly Matcher _explicitlyIncludedPaths;

    public PipelinePathFilter(PipelineDefinition.PathDefinition pathDefinition)
    {
        var includedPaths = pathDefinition?.Include ?? Array.Empty<string>();
        var excludedPaths = pathDefinition?.Exclude ?? Array.Empty<string>();

        // The no-op pipeline only needs the filter shape used by ultimate-pipeline.yml:
        // include everything, exclude selected paths, then re-include exact files beneath
        // excluded directories. Reject more complex filters instead of silently disagreeing
        // with Azure Pipelines and reporting successful status checks for a running pipeline.
        if (includedPaths.Length > 0 && !includedPaths.Contains(MatchAllPattern, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Pipeline path includes must contain '{MatchAllPattern}'.");
        }

        var explicitlyIncludedPaths = includedPaths.Where(path => path != MatchAllPattern).ToArray();
        foreach (var path in explicitlyIncludedPaths)
        {
            if (ContainsWildcard(path))
            {
                throw new InvalidOperationException($"Re-included pipeline path '{path}' must be an exact path.");
            }

            if (!excludedPaths.Any(excludedPath => IsChildPath(path, excludedPath)))
            {
                throw new InvalidOperationException($"Re-included pipeline path '{path}' must be beneath an excluded directory.");
            }
        }

        _excludedPaths = CreateMatcher(excludedPaths);
        _explicitlyIncludedPaths = CreateMatcher(explicitlyIncludedPaths);
    }

    public bool IsExcluded(string path)
        => _excludedPaths.Match(path).HasMatches && !_explicitlyIncludedPaths.Match(path).HasMatches;

    private static Matcher CreateMatcher(string[] paths)
    {
        var matcher = new Matcher(StringComparison.Ordinal);
        foreach (var path in paths)
        {
            // Azure treats a trailing slash as the directory and all of its descendants.
            matcher.AddInclude(path.EndsWith("/", StringComparison.Ordinal) ? path + MatchAllPattern : path);
        }

        return matcher;
    }

    private static bool ContainsWildcard(string path)
        => path.IndexOf('*') >= 0 || path.IndexOf('?') >= 0;

    private static bool IsChildPath(string path, string directoryPath)
    {
        if (!directoryPath.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return path.StartsWith(directoryPath, StringComparison.Ordinal);
    }
}
