// <copyright file="CIEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

// ReSharper disable once InconsistentNaming

internal abstract class CIEnvironmentValues
{
    private const int CodeOwnersSearchCacheLimit = 256;
    internal const string RepositoryUrlPattern = @"((http|git|ssh|http(s)|file|\/?)|(git@[\w\.\-]+))(:(\/\/)?)([\w\.@\:/\-~]+)(\.git)?(\/)?";
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIEnvironmentValues));
    private static readonly Lazy<CIEnvironmentValues> LazyInstance = new(Create);
    private static readonly Regex BranchOrTagsRegex = new(@"^refs\/heads\/tags\/(.*)|refs\/heads\/(.*)|refs\/tags\/(.*)|refs\/(.*)|origin\/tags\/(.*)|origin\/(.*)$", RegexOptions.Compiled);
    private static readonly StringComparer CodeOwnersSearchComparer = FrameworkDescription.Instance.IsWindows()
                                                                          ? StringComparer.OrdinalIgnoreCase
                                                                          : StringComparer.Ordinal;

    private readonly object _codeOwnersLock = new();
    private readonly HashSet<string> _codeOwnersSearchStarts = new(CodeOwnersSearchComparer);

    private string? _gitSearchFolder = null;

    public static CIEnvironmentValues Instance => LazyInstance.Value;

    public string? GitSearchFolder
    {
        get => _gitSearchFolder;
        set
        {
            _gitSearchFolder = value;
            ReloadEnvironmentData();
        }
    }

    public bool IsCI { get; protected set; }

    public string? Provider { get; protected set; }

    public string? Repository { get; protected set; }

    public string? Commit { get; protected set; }

    public string? Branch { get; protected set; }

    public string? Tag { get; protected set; }

    public string? AuthorName { get; protected set; }

    public string? AuthorEmail { get; protected set; }

    public DateTimeOffset? AuthorDate { get; protected set; }

    public string? CommitterName { get; protected set; }

    public string? CommitterEmail { get; protected set; }

    public DateTimeOffset? CommitterDate { get; protected set; }

    public string? Message { get; protected set; }

    public string? SourceRoot { get; protected set; }

    public string? PipelineId { get; protected set; }

    public string? PipelineName { get; protected set; }

    public string? PipelineNumber { get; protected set; }

    public string? PipelineUrl { get; protected set; }

    public string? JobUrl { get; protected set; }

    public string? JobName { get; protected set; }

    public string? JobId { get; protected set; }

    public string? StageName { get; protected set; }

    public string? WorkspacePath { get; protected set; }

    public string? NodeName { get; protected set; }

    public string[]? NodeLabels { get; protected set; }

    public string? PrBaseCommit { get; protected set; }

    public string? PrBaseHeadCommit { get; protected set; }

    public string? PrBaseBranch { get; protected set; }

    public string? PrNumber { get; protected set; }

    public string? HeadCommit { get; protected set; }

    public string? HeadAuthorName { get; protected set; }

    public string? HeadAuthorEmail { get; protected set; }

    public DateTimeOffset? HeadAuthorDate { get; protected set; }

    public string? HeadCommitterName { get; protected set; }

    public string? HeadCommitterEmail { get; protected set; }

    public DateTimeOffset? HeadCommitterDate { get; protected set; }

    public string? HeadMessage { get; protected set; }

    public CodeOwners? CodeOwners { get; protected set; }

    internal string? CodeOwnersRoot { get; private set; }

    public Dictionary<string, string?>? VariablesToBypass { get; protected set; }

    public MetricTags.CIVisibilityTestSessionProvider MetricTag { get; protected set; } = MetricTags.CIVisibilityTestSessionProvider.Unsupported;

    public static CIEnvironmentValues Create()
    {
        var values = CIEnvironmentValues<EnvironmentVariablesProvider>.Create(new EnvironmentVariablesProvider());
        values.ReloadEnvironmentData();
        return values;
    }

    public static CIEnvironmentValues Create(Dictionary<string, string> source)
    {
        var values = CIEnvironmentValues<DictionaryValuesProvider>.Create(new DictionaryValuesProvider(source));
        values.ReloadEnvironmentData();
        return values;
    }

    public static string? RemoveSensitiveInformationFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var value = uri.GetComponents(UriComponents.Fragment | UriComponents.Query | UriComponents.Path | UriComponents.Port | UriComponents.Host | UriComponents.Scheme, UriFormat.SafeUnescaped);
                // In some cases `GetComponents` introduces a slash at the end of the url
                if (!url!.EndsWith("/") && value.EndsWith("/"))
                {
                    value = value.Substring(0, value.Length - 1);
                }

                return value;
            }
        }
        else
        {
            var urlPattern = new Regex("^(ssh://)(.*@)(.*)");
            var urlMatch = urlPattern.Match(url);
            if (urlMatch.Success)
            {
                url = urlMatch.Result("$1$3");
            }
        }

        return url;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetTagIfNotNullOrEmpty(Span span, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            span.SetTag(key, value);
        }
    }

    protected static bool IsHex(IEnumerable<char> chars)
    {
        foreach (var c in chars)
        {
            var isHex = (c is >= '0' and <= '9' ||
                         c is >= 'a' and <= 'f' ||
                         c is >= 'A' and <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    internal static string? CleanTagValue(string? tag)
    {
        try
        {
            // Clean tag name
            if (!string.IsNullOrEmpty(tag))
            {
                var match = BranchOrTagsRegex.Match(tag);
                if (match is { Success: true, Groups.Count: 7 })
                {
                    tag =
                        !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? match.Groups[3].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[5].Value) ? match.Groups[5].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                                                                            match.Groups[4].Value;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error fixing tag name: {TagName}", tag);
        }

        return tag;
    }

    internal static Tuple<string?, string?> CleanBranchValue(string? branch)
    {
        string? tag = null;
        try
        {
            // Clean branch name
            if (!string.IsNullOrEmpty(branch))
            {
                var match = BranchOrTagsRegex.Match(branch);
                if (match is { Success: true, Groups.Count: 7 })
                {
                    branch =
                        !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? match.Groups[4].Value :
                                                                            match.Groups[6].Value;
                    tag =
                        !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? match.Groups[3].Value :
                                                                            match.Groups[5].Value;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error fixing branch name: {BranchName}", branch);
        }

        return Tuple.Create(branch, tag);
    }

    private static string? GetCodeOwnersSearchStart(string? path, string? basePath)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string? resolvedPath = null;
        try
        {
            if (Path.IsPathRooted(path) || Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                resolvedPath = path;
            }
            else if (!StringUtil.IsNullOrWhiteSpace(basePath) && Path.IsPathRooted(basePath))
            {
                // Keep relative paths anchored to a known workspace and reject escapes (no CWD fallback).
                TryResolvePathWithinBase(path, basePath, out resolvedPath);
            }

            if (StringUtil.IsNullOrWhiteSpace(resolvedPath))
            {
                return null;
            }

            // Start searching from the directory containing the candidate path.
            if (Directory.Exists(resolvedPath))
            {
                return resolvedPath;
            }

            return Path.GetDirectoryName(resolvedPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search start for '{Path}'", resolvedPath ?? path);
            return null;
        }
    }

    private static bool HasGitDirectory(string path)
    {
        var gitPath = Path.Combine(path, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private static bool TryResolvePathWithinBase(string relativePath, string basePath, [NotNullWhen(true)] out string? absolutePath)
    {
        absolutePath = null;

        if (StringUtil.IsNullOrWhiteSpace(relativePath) || StringUtil.IsNullOrWhiteSpace(basePath))
        {
            return false;
        }

        try
        {
            // Only combine relative paths; rooted or absolute inputs bypass base anchoring.
            if (Path.IsPathRooted(relativePath) || Uri.TryCreate(relativePath, UriKind.Absolute, out _))
            {
                return false;
            }

            if (!Path.IsPathRooted(basePath))
            {
                return false;
            }

            // Normalize to full paths and ensure the combined path stays within the base.
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var fullBasePath = Path.GetFullPath(basePath);
            var fullBasePathWithSeparator = fullBasePath;
            if (!fullBasePathWithSeparator.EndsWith(Path.DirectorySeparatorChar.ToString(), comparison) &&
                !fullBasePathWithSeparator.EndsWith(Path.AltDirectorySeparatorChar.ToString(), comparison))
            {
                fullBasePathWithSeparator += Path.DirectorySeparatorChar;
            }

            var combinedPath = Path.Combine(fullBasePath, relativePath);
            var fullCombinedPath = Path.GetFullPath(combinedPath);
            // Reject traversal that escapes the base directory.
            if (!fullCombinedPath.StartsWith(fullBasePathWithSeparator, comparison) &&
                !string.Equals(fullCombinedPath, fullBasePath, comparison))
            {
                return false;
            }

            absolutePath = fullCombinedPath;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving relative path '{Path}' within base '{BasePath}'", relativePath, basePath);
        }

        return false;
    }

    private static bool TryGetCodeOwnersPath(string sourceRoot, bool logLookup, [NotNullWhen(true)] out string? codeOwnersPath)
    {
        foreach (var path in GetCodeOwnersPaths(sourceRoot))
        {
            if (logLookup)
            {
                Log.Debug("Looking for CODEOWNERS file in: {Path}", path);
            }

            if (File.Exists(path))
            {
                codeOwnersPath = path;
                return true;
            }
        }

        codeOwnersPath = null;
        return false;
    }

    private static IEnumerable<string> GetCodeOwnersPaths(string sourceRoot)
    {
        yield return Path.Combine(sourceRoot, "CODEOWNERS");
        yield return Path.Combine(sourceRoot, ".github", "CODEOWNERS");
        yield return Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS");
        yield return Path.Combine(sourceRoot, ".docs", "CODEOWNERS");
    }

    public void DecorateSpan(Span span)
    {
        if (span == null)
        {
            return;
        }

        SetTagIfNotNullOrEmpty(span, CommonTags.CIProvider, Provider);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitRepository, Repository);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommit, Commit);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitBranch, Branch);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitTag, Tag);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorName, AuthorName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorEmail, AuthorEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorDate, AuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterName, CommitterName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterEmail, CommitterEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterDate, CommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitMessage, Message);
        SetTagIfNotNullOrEmpty(span, CommonTags.BuildSourceRoot, SourceRoot);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineId, PipelineId);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineName, PipelineName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineNumber, PipelineNumber);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineUrl, PipelineUrl);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIJobUrl, JobUrl);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIJobName, JobName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIJobId, JobId);
        SetTagIfNotNullOrEmpty(span, CommonTags.StageName, StageName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIWorkspacePath, WorkspacePath);
        SetTagIfNotNullOrEmpty(span, CommonTags.CINodeName, NodeName);
        if (NodeLabels is { } nodeLabels)
        {
            SetTagIfNotNullOrEmpty(span, CommonTags.CINodeLabels, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(nodeLabels));
        }

        SetTagIfNotNullOrEmpty(span, CommonTags.GitPrBaseHeadCommit, PrBaseHeadCommit);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitPrBaseCommit, PrBaseCommit);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitPrBaseBranch, PrBaseBranch);
        SetTagIfNotNullOrEmpty(span, CommonTags.PrNumber, PrNumber);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommit, HeadCommit);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitAuthorDate, HeadAuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitAuthorName, HeadAuthorName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitAuthorEmail, HeadAuthorEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitCommitterDate, HeadCommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitCommitterName, HeadCommitterName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitCommitterEmail, HeadCommitterEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitHeadCommitMessage, HeadMessage);

        if (VariablesToBypass is { } variablesToBypass)
        {
            span.SetTag(CommonTags.CiEnvVars, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass));
        }
    }

    protected void ReloadEnvironmentData()
    {
        // **********
        // Setup variables
        // **********
        Log.Information("CIEnvironmentValues: Loading environment data.");

        Provider = null;
        PipelineId = null;
        PipelineName = null;
        PipelineNumber = null;
        PipelineUrl = null;
        JobUrl = null;
        JobName = null;
        JobId = null;
        StageName = null;
        WorkspacePath = null;
        Repository = null;
        Commit = null;
        Branch = null;
        Tag = null;
        AuthorName = null;
        AuthorEmail = null;
        AuthorDate = null;
        CommitterName = null;
        CommitterEmail = null;
        CommitterDate = null;
        Message = null;
        SourceRoot = null;
        CodeOwners = null;
        CodeOwnersRoot = null;
        lock (_codeOwnersLock)
        {
            _codeOwnersSearchStarts.Clear();
        }

        Setup(string.IsNullOrEmpty(_gitSearchFolder) ? GitInfo.GetCurrent() : GitInfo.GetFrom(_gitSearchFolder!));

        // **********
        // Remove sensitive info from repository url
        // **********
        Repository = RemoveSensitiveInformationFromUrl(Repository);

        // **********
        // Clean Refs
        // **********

        CleanBranchAndTag();

        // **********
        // Sanitize Repository Url (Remove username:password info from the url)
        // **********
        if (!string.IsNullOrEmpty(Repository) &&
            Uri.TryCreate(Repository, UriKind.Absolute, out var uriRepository) &&
            !string.IsNullOrEmpty(uriRepository.UserInfo))
        {
            Repository = Repository!.Replace(uriRepository.UserInfo + "@", string.Empty);
            Repository = Repository.Replace(uriRepository.UserInfo, string.Empty);
        }

        // **********
        // Try load CodeOwners
        // **********
        if (!string.IsNullOrEmpty(SourceRoot))
        {
            if (TryGetCodeOwnersPath(SourceRoot!, logLookup: true, out var codeOwnersPath))
            {
                Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);
                CodeOwners = new CodeOwners(codeOwnersPath, GetCodeOwnersPlatform());
                CodeOwnersRoot = SourceRoot;
            }
        }
    }

    protected abstract void Setup(IGitInfo gitInfo);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CleanBranchAndTag()
    {
        Tag = CleanTagValue(Tag);
        var branchTag = CleanBranchValue(Branch);
        Branch = branchTag.Item1;
        if (string.IsNullOrEmpty(Tag))
        {
            Tag = branchTag.Item2;
        }

        PrBaseBranch = CleanBranchValue(PrBaseBranch).Item1;

        if (string.IsNullOrEmpty(Tag))
        {
            Tag = null;
        }

        if (string.IsNullOrEmpty(Branch))
        {
            Branch = null;
        }

        if (string.IsNullOrEmpty(PrBaseBranch))
        {
            PrBaseBranch = null;
        }
    }

    public string MakeRelativePathFromSourceRoot(string absolutePath, bool useOSSeparator = true)
    {
        return MakeRelativePath(SourceRoot, absolutePath, useOSSeparator);
    }

    internal string MakeRelativePathFromSourceRootWithFallback(string sourceFilePath, bool useOSSeparator = true)
    {
        var sourceRelativePath = MakeRelativePathFromSourceRoot(sourceFilePath, useOSSeparator);
        // If CODEOWNERS is rooted elsewhere, normalize SourceFile to that root for consistent matching.
        if (TryGetCodeOwnersRelativePath(sourceFilePath, useOSSeparator, out var codeOwnersRelativePath))
        {
            return codeOwnersRelativePath;
        }

        return sourceRelativePath;
    }

    internal bool TryGetCodeOwnersRelativePath(string sourceFilePath, bool useOSSeparator, [NotNullWhen(true)] out string? codeOwnersRelativePath)
    {
        codeOwnersRelativePath = null;

        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        // Algorithm: load CODEOWNERS (with fallback), resolve roots, then normalize source file to repo-relative.
        // Ensure CODEOWNERS is loaded (or discovered via fallback) before attempting normalization.
        EnsureCodeOwnersFromFallback(sourceFilePath);

        if (CodeOwners is null || StringUtil.IsNullOrWhiteSpace(CodeOwnersRoot))
        {
            return false;
        }

        var codeOwnersRoot = CodeOwnersRoot!;
        if (!Path.IsPathRooted(codeOwnersRoot))
        {
            // If SourceRoot was relative, re-anchor to WorkspacePath before matching.
            if (StringUtil.IsNullOrWhiteSpace(WorkspacePath) ||
                !TryResolvePathWithinBase(codeOwnersRoot, WorkspacePath!, out var resolvedRoot))
            {
                return false;
            }

            // Require a CODEOWNERS file at the resolved root to avoid mismatched roots.
            // Avoid mixing CODEOWNERS content from one root with a different resolved root.
            if (!TryGetCodeOwnersPath(resolvedRoot, logLookup: false, out _))
            {
                return false;
            }

            codeOwnersRoot = resolvedRoot;
        }

        // Only match when the source file can be resolved under the CODEOWNERS root.
        string absolutePath;
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            // Absolute paths are already resolved, no workspace anchoring needed.
            absolutePath = sourceFilePath;
        }
        else
        {
            // For relative paths, enforce that they stay within the CODEOWNERS root.
            // Relative paths must stay within the codeowners root; otherwise we skip.
            if (!TryResolvePathWithinBase(sourceFilePath, codeOwnersRoot, out var resolvedPath))
            {
                return false;
            }

            absolutePath = resolvedPath;
        }

        // Normalize to a repo-relative path before matching the CODEOWNERS rules.
        var relativePath = MakeRelativePath(codeOwnersRoot, absolutePath, useOSSeparator);
        // Guard against paths that escape the root or remain absolute after normalization.
        if (StringUtil.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            Uri.TryCreate(relativePath, UriKind.Absolute, out _) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.StartsWith("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        codeOwnersRelativePath = relativePath;
        return true;
    }

    private string MakeRelativePath(string? basePath, string absolutePath, bool useOSSeparator)
    {
        var pivotFolder = basePath;
        if (StringUtil.IsNullOrEmpty(pivotFolder))
        {
            return absolutePath;
        }

        if (StringUtil.IsNullOrEmpty(absolutePath))
        {
            return pivotFolder!;
        }

        try
        {
            // Use Uri to normalize and compute the relative path across OS separators.
            var folderSeparator = Path.DirectorySeparatorChar;
            if (pivotFolder![pivotFolder.Length - 1] != folderSeparator)
            {
                pivotFolder += folderSeparator;
            }

            var pivotFolderUri = new Uri(pivotFolder);
            var absolutePathUri = new Uri(absolutePath);
            var relativeUri = pivotFolderUri.MakeRelativeUri(absolutePathUri);
            if (useOSSeparator)
            {
                return Uri.UnescapeDataString(
                    relativeUri.ToString().Replace('/', folderSeparator));
            }

            return Uri.UnescapeDataString(relativeUri.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error creating a relative path for '{AbsolutePath}' from '{BasePath}'", absolutePath, pivotFolder);
        }

        return absolutePath;
    }

    private void EnsureCodeOwnersFromFallback(string? sourceFilePath)
    {
        if (CodeOwners is not null)
        {
            return;
        }

        lock (_codeOwnersLock)
        {
            if (CodeOwners is not null)
            {
                return;
            }

            // Search order: source file path (most specific), then workspace root.
            // Prefer a source-file-anchored search before falling back to the workspace root.
            var platform = GetCodeOwnersPlatform();
            if (TryLoadCodeOwnersFromAncestor(sourceFilePath, platform, WorkspacePath))
            {
                return;
            }

            TryLoadCodeOwnersFromAncestor(WorkspacePath, platform, basePath: null);
        }
    }

    private bool TryLoadCodeOwnersFromAncestor(string? startPath, CodeOwners.Platform platform, string? basePath)
    {
        var startDirectory = GetCodeOwnersSearchStart(startPath, basePath);
        if (StringUtil.IsNullOrEmpty(startDirectory))
        {
            return false;
        }

        DirectoryInfo? directoryInfo;
        try
        {
            directoryInfo = new DirectoryInfo(startDirectory);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search directory from '{Path}'", startDirectory);
            return false;
        }

        // Limit cache growth to avoid unbounded memory in large test suites.
        if (_codeOwnersSearchStarts.Count >= CodeOwnersSearchCacheLimit)
        {
            _codeOwnersSearchStarts.Clear();
        }

        // Skip repeated lookups for the same starting directory.
        if (!_codeOwnersSearchStarts.Add(directoryInfo.FullName))
        {
            return false;
        }

        // Walk parent directories until we find CODEOWNERS or hit a git boundary.
        while (directoryInfo != null)
        {
            if (TryGetCodeOwnersPath(directoryInfo.FullName, logLookup: false, out var codeOwnersPath))
            {
                Log.Information("CODEOWNERS file found using fallback search: {Path}", codeOwnersPath);
                CodeOwners = new CodeOwners(codeOwnersPath, platform);
                CodeOwnersRoot = directoryInfo.FullName;
                return true;
            }

            // Stop walking when we hit a git boundary.
            if (HasGitDirectory(directoryInfo.FullName))
            {
                break;
            }

            directoryInfo = directoryInfo.Parent;
        }

        return false;
    }

    private CodeOwners.Platform GetCodeOwnersPlatform()
        => GetType().Name.Contains("GitlabEnvironmentValues") ? CodeOwners.Platform.GitLab : CodeOwners.Platform.GitHub;
}
