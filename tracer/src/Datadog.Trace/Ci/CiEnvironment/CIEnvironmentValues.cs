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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.CiEnvironment;

// ReSharper disable once InconsistentNaming

internal abstract class CIEnvironmentValues
{
    private const int CodeOwnersSearchCacheLimit = 256;
    internal const string RepositoryUrlPattern = @"((http|git|ssh|http(s)|file|\/?)|(git@[\w\.\-]+))(:(\/\/)?)([\w\.@\:/\-~]+)(\.git)?(\/)?";
    internal static readonly TimeSpan CodeOwnersLoadFailureRetryDelay = TimeSpan.FromSeconds(30);
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIEnvironmentValues));
    private static readonly Lazy<CIEnvironmentValues> LazyInstance = new(Create);
    private static readonly Regex BranchOrTagsRegex = new(@"^refs\/heads\/tags\/(.*)|refs\/heads\/(.*)|refs\/tags\/(.*)|refs\/(.*)|origin\/tags\/(.*)|origin\/(.*)$", RegexOptions.Compiled);
    private static readonly StringComparer CodeOwnersSearchComparer = FrameworkDescription.Instance.IsWindows()
                                                                 ? StringComparer.OrdinalIgnoreCase
                                                                 : StringComparer.Ordinal;

    private static readonly char[] ForwardSlashCharacters = { '/' };

    private readonly object _codeOwnersLock = new();
    private readonly Dictionary<string, LinkedListNode<CodeOwnersSearchCacheEntry>> _codeOwnersSearchCache = new(CodeOwnersSearchComparer);
    private readonly LinkedList<CodeOwnersSearchCacheEntry> _codeOwnersSearchCacheOrder = new();

    private CodeOwnersState? _codeOwnersState;
    private int _environmentReloadVersion;
    private string? _gitSearchFolder;

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

    public string? PipelineDisplayName { get; protected set; }

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

    public CodeOwners? CodeOwners => Volatile.Read(ref _codeOwnersState)?.Parser;

    internal string? CodeOwnersRoot => Volatile.Read(ref _codeOwnersState)?.Root;

    // Test-only synchronization hooks. They are null in production and run only on the uncommon
    // paths that wait for an active reload or perform fallback discovery.
    internal Action? BeforeCodeOwnersReloadWait { get; set; }

    internal Action? BeforeCodeOwnersFallbackLock { get; set; }

    internal Action? CodeOwnersFallbackSearchStarting { get; set; }

    internal Func<DateTime>? CodeOwnersUtcNowProvider { get; set; }

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
            if (!char.IsAsciiHexDigit(c))
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

    private static string? GetCodeOwnersSearchBoundary(DirectoryInfo startDirectory, string? workspacePath)
    {
        // A real git boundary takes precedence, including when the CI workspace points at a
        // subdirectory of the checkout.
        for (var current = startDirectory; current is not null; current = current.Parent)
        {
            if (HasGitDirectory(current.FullName))
            {
                return current.FullName;
            }
        }

        if (StringUtil.IsNullOrWhiteSpace(workspacePath) || !Path.IsPathRooted(workspacePath))
        {
            return null;
        }

        try
        {
            var fullWorkspacePath = Path.GetFullPath(workspacePath!);
            var fullStartPath = Path.GetFullPath(startDirectory.FullName);
            if (CodeOwnersSearchComparer.Equals(fullStartPath, fullWorkspacePath))
            {
                return fullWorkspacePath;
            }

            var workspaceWithSeparator = fullWorkspacePath;
            if (!workspaceWithSeparator.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !workspaceWithSeparator.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                workspaceWithSeparator += Path.DirectorySeparatorChar;
            }

            var comparison = FrameworkDescription.Instance.IsWindows()
                                 ? StringComparison.OrdinalIgnoreCase
                                 : StringComparison.Ordinal;
            return fullStartPath.StartsWith(workspaceWithSeparator, comparison) ? fullWorkspacePath : null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS workspace boundary from '{Path}'", workspacePath);
            return null;
        }
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

    private static bool TryGetCodeOwnersPath(string sourceRoot, CodeOwners.Platform platform, bool logLookup, [NotNullWhen(true)] out string? codeOwnersPath)
    {
        foreach (var path in GetCodeOwnersPaths(sourceRoot, platform))
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

    private static IEnumerable<string> GetCodeOwnersPaths(string sourceRoot, CodeOwners.Platform platform)
    {
        if (platform == CodeOwners.Platform.GitHub)
        {
            // GitHub searches .github first, then the repository root, then docs.
            yield return Path.Combine(sourceRoot, ".github", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "docs", "CODEOWNERS");
        }
        else
        {
            // GitLab searches the repository root first, then docs, then .gitlab.
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "docs", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS");
        }
    }

    private static CodeOwnersFileMetadata GetCodeOwnersFileMetadata(string path)
    {
        try
        {
            var file = new FileInfo(path);
            file.Refresh();
            return file.Exists
                       ? new CodeOwnersFileMetadata(exists: true, file.Length, file.LastWriteTimeUtc.Ticks)
                       : default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return default;
        }
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
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineDisplayName, PipelineDisplayName);
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
            SetTagIfNotNullOrEmpty(span, CommonTags.CINodeLabels, JsonHelper.SerializeObject(nodeLabels));
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
            span.SetTag(CommonTags.CiEnvVars, JsonHelper.SerializeObject(variablesToBypass));
        }
    }

    protected void ReloadEnvironmentData()
    {
        // Reload changes the source root and its CODEOWNERS parser as one logical state transition.
        // Serialize the complete transition with fallback discovery so neither can publish state
        // derived from a root that the other operation is replacing.
        lock (_codeOwnersLock)
        {
            Interlocked.Increment(ref _environmentReloadVersion);
            try
            {
                ReloadEnvironmentDataCore();
            }
            finally
            {
                Interlocked.Increment(ref _environmentReloadVersion);
            }
        }
    }

    private void ReloadEnvironmentDataCore()
    {
        // **********
        // Setup variables
        // **********
        Log.Information("CIEnvironmentValues: Loading environment data.");

        Provider = null;
        PipelineId = null;
        PipelineName = null;
        PipelineDisplayName = null;
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
        Volatile.Write(ref _codeOwnersState, null);
        ClearCodeOwnersSearchCache();

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
            var platform = GetCodeOwnersPlatform();
            if (TryGetCodeOwnersPath(SourceRoot!, platform, logLookup: true, out var codeOwnersPath))
            {
                Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);
                TryPublishCodeOwners(codeOwnersPath, platform, SourceRoot!);
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
        => MakeRelativePathFromSourceRootWithFallback(sourceFilePath, useOSSeparator, out _);

    internal string MakeRelativePathFromSourceRootWithFallback(string sourceFilePath, bool useOSSeparator, out string[] codeOwners)
    {
        // The normal path stays lock-free. A version change makes the operation retry, while an
        // active reload waits on the same lock used for the state transition. This ensures that
        // SourceRoot, the relative path, and CODEOWNERS all come from one completed reload.
        while (true)
        {
            var reloadVersion = Volatile.Read(ref _environmentReloadVersion);
            if ((reloadVersion & 1) != 0)
            {
                BeforeCodeOwnersReloadWait?.Invoke();
                lock (_codeOwnersLock)
                {
                }

                continue;
            }

            var sourceRelativePath = MakeRelativePathFromSourceRoot(sourceFilePath, useOSSeparator);
            string result;
            string[] matchedOwners;
            if (TryGetCodeOwnersRelativePath(sourceFilePath, useOSSeparator, out var codeOwnersRelativePath, out var parser))
            {
                result = codeOwnersRelativePath;
                matchedOwners = parser.Match("/" + codeOwnersRelativePath).ToArray();
            }
            else
            {
                result = sourceRelativePath;
                matchedOwners = [];
            }

            if (reloadVersion == Volatile.Read(ref _environmentReloadVersion))
            {
                codeOwners = matchedOwners;
                return result;
            }
        }
    }

    internal bool TryGetCodeOwnersRelativePath(string sourceFilePath, bool useOSSeparator, [NotNullWhen(true)] out string? codeOwnersRelativePath)
        => TryGetCodeOwnersRelativePath(sourceFilePath, useOSSeparator, out codeOwnersRelativePath, out _);

    private bool TryGetCodeOwnersRelativePath(
        string sourceFilePath,
        bool useOSSeparator,
        [NotNullWhen(true)] out string? codeOwnersRelativePath,
        [NotNullWhen(true)] out CodeOwners? parser)
    {
        codeOwnersRelativePath = null;
        parser = null;

        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        // Algorithm: load CODEOWNERS (with fallback), resolve roots, then normalize source file to repo-relative.
        // Ensure CODEOWNERS is loaded (or discovered via fallback) before attempting normalization.
        EnsureCodeOwnersFromFallback(sourceFilePath);

        var codeOwnersState = Volatile.Read(ref _codeOwnersState);
        if (codeOwnersState is null || StringUtil.IsNullOrWhiteSpace(codeOwnersState.Root))
        {
            return false;
        }

        var codeOwnersRoot = codeOwnersState.Root;
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
            if (!TryGetCodeOwnersPath(resolvedRoot, codeOwnersState.Platform, logLookup: false, out _))
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
            // Relative paths must stay within the codeowners root; otherwise we try to anchor them.
            if (!TryResolvePathWithinBase(sourceFilePath, codeOwnersRoot, out var resolvedPath))
            {
                var anchored = TryAnchorPathToCodeOwnersRoot(sourceFilePath, codeOwnersRoot, useOSSeparator, out codeOwnersRelativePath);
                if (anchored)
                {
                    parser = codeOwnersState.Parser;
                }

                return anchored;
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
            var anchored = TryAnchorPathToCodeOwnersRoot(sourceFilePath, codeOwnersRoot, useOSSeparator, out codeOwnersRelativePath);
            if (anchored)
            {
                parser = codeOwnersState.Parser;
            }

            return anchored;
        }

        codeOwnersRelativePath = relativePath;
        parser = codeOwnersState.Parser;
        return true;
    }

    private bool TryAnchorPathToCodeOwnersRoot(string sourceFilePath, string codeOwnersRoot, bool useOSSeparator, [NotNullWhen(true)] out string? codeOwnersRelativePath)
    {
        // Compiler-recorded paths can be relative to a different base directory than the current
        // workspace (e.g. "../../../_/tracer/test/SampleTests.cs" on CI agents). When strict
        // resolution fails, anchor the path by finding the longest suffix that exists under the
        // CODEOWNERS root, independently of the CI provider layout that produced the prefix.
        codeOwnersRelativePath = null;
        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath) ||
            Path.IsPathRooted(sourceFilePath) ||
            Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            // Only relative paths recorded against a foreign base directory are anchored; absolute
            // paths pointing outside the repository must not be re-anchored into it.
            return false;
        }

        var normalizedPath = sourceFilePath.Replace('\\', '/');
        var pathWithoutForeignPrefix = normalizedPath;
        while (pathWithoutForeignPrefix.StartsWith("../", StringComparison.Ordinal) ||
               pathWithoutForeignPrefix.StartsWith("./", StringComparison.Ordinal))
        {
            var prefixLength = pathWithoutForeignPrefix.StartsWith("../", StringComparison.Ordinal) ? 3 : 2;
            pathWithoutForeignPrefix = pathWithoutForeignPrefix.Substring(prefixLength);
        }

        if (Path.IsPathRooted(pathWithoutForeignPrefix) || Uri.TryCreate(pathWithoutForeignPrefix, UriKind.Absolute, out _))
        {
            // A drive, UNC path, Unix root, or URI embedded after navigation segments is still
            // absolute. Reject the whole source path instead of matching a shorter local suffix.
            return false;
        }

        var segments = normalizedPath.Split(ForwardSlashCharacters, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            // Never anchor bare file names: too easy to match an unrelated file.
            return false;
        }

        // Skip leading "." / ".." navigation segments: they belong to the foreign base directory.
        var start = 0;
        while (start < segments.Length && (segments[start] == "." || segments[start] == ".."))
        {
            start++;
        }

        // Never anchor paths with interior navigation segments: their resolution depends on the
        // unknown base directory and would produce malformed repository-relative paths.
        for (var i = start; i < segments.Length; i++)
        {
            if (segments[i] == "." || segments[i] == "..")
            {
                return false;
            }
        }

        for (var i = start; i < segments.Length - 1; i++)
        {
            var candidateSuffix = string.Join(Path.DirectorySeparatorChar.ToString(), segments, i, segments.Length - i);
            if (TryResolvePathWithinBase(candidateSuffix, codeOwnersRoot, out var candidatePath) && File.Exists(candidatePath))
            {
                var separator = useOSSeparator ? Path.DirectorySeparatorChar.ToString() : "/";
                codeOwnersRelativePath = string.Join(separator, segments, i, segments.Length - i);
                return true;
            }
        }

        return false;
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
        if (Volatile.Read(ref _codeOwnersState) is not null)
        {
            return;
        }

        BeforeCodeOwnersFallbackLock?.Invoke();
        lock (_codeOwnersLock)
        {
            if (Volatile.Read(ref _codeOwnersState) is not null)
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

            TryLoadCodeOwnersFromAncestor(WorkspacePath, platform, WorkspacePath);
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

        var repositoryBoundary = GetCodeOwnersSearchBoundary(directoryInfo, basePath);
        var searchCacheKey = repositoryBoundary ?? directoryInfo.FullName;
        if (ShouldSkipCodeOwnersSearch(searchCacheKey))
        {
            return false;
        }

        CodeOwnersFallbackSearchStarting?.Invoke();
        string? nearestCodeOwnersPath = null;
        string? nearestCodeOwnersRoot = null;

        // When a repository boundary exists, only its repository-level CODEOWNERS locations are
        // valid. If git metadata is unavailable, a containing workspace is the safest boundary.
        // Retain the nearest candidate solely when neither boundary can be discovered.
        while (directoryInfo != null)
        {
            var isRepositoryBoundary = repositoryBoundary is not null &&
                                       CodeOwnersSearchComparer.Equals(directoryInfo.FullName, repositoryBoundary);
            if (TryGetCodeOwnersPath(directoryInfo.FullName, platform, logLookup: false, out var codeOwnersPath))
            {
                if (isRepositoryBoundary)
                {
                    if (PublishFallbackCodeOwners(codeOwnersPath, platform, directoryInfo.FullName))
                    {
                        return true;
                    }

                    CacheCodeOwnersLoadFailure(searchCacheKey, codeOwnersPath);
                    return false;
                }

                nearestCodeOwnersPath ??= codeOwnersPath;
                nearestCodeOwnersRoot ??= directoryInfo.FullName;
            }

            if (isRepositoryBoundary)
            {
                // A nested CODEOWNERS candidate is not valid for this repository. Do not fall
                // through to it when the actual repository root has no CODEOWNERS file.
                CacheCodeOwnersSearch(searchCacheKey, failedCodeOwnersPath: null);
                return false;
            }

            directoryInfo = directoryInfo.Parent;
        }

        if (nearestCodeOwnersPath is not null && nearestCodeOwnersRoot is not null)
        {
            if (PublishFallbackCodeOwners(nearestCodeOwnersPath, platform, nearestCodeOwnersRoot))
            {
                return true;
            }

            CacheCodeOwnersLoadFailure(searchCacheKey, nearestCodeOwnersPath);
            return false;
        }

        CacheCodeOwnersSearch(searchCacheKey, failedCodeOwnersPath: null);
        return false;
    }

    private bool ShouldSkipCodeOwnersSearch(string searchCacheKey)
    {
        if (!_codeOwnersSearchCache.TryGetValue(searchCacheKey, out var node))
        {
            return false;
        }

        var entry = node.Value;
        if (entry.FailedCodeOwnersPath is null)
        {
            return true;
        }

        var metadataUnchanged = entry.FileMetadata.Equals(GetCodeOwnersFileMetadata(entry.FailedCodeOwnersPath));
        if (metadataUnchanged && GetCodeOwnersUtcNow() < entry.RetryAfterUtc)
        {
            return true;
        }

        RemoveCodeOwnersSearchCacheEntry(node);
        return false;
    }

    private void CacheCodeOwnersLoadFailure(string searchCacheKey, string codeOwnersPath)
        => CacheCodeOwnersSearch(searchCacheKey, codeOwnersPath);

    private void CacheCodeOwnersSearch(string searchCacheKey, string? failedCodeOwnersPath)
    {
        if (_codeOwnersSearchCache.TryGetValue(searchCacheKey, out var existingNode))
        {
            RemoveCodeOwnersSearchCacheEntry(existingNode);
        }

        while (_codeOwnersSearchCache.Count >= CodeOwnersSearchCacheLimit)
        {
            RemoveCodeOwnersSearchCacheEntry(_codeOwnersSearchCacheOrder.First!);
        }

        var entry = new CodeOwnersSearchCacheEntry(
            searchCacheKey,
            failedCodeOwnersPath,
            failedCodeOwnersPath is null ? default : GetCodeOwnersFileMetadata(failedCodeOwnersPath),
            failedCodeOwnersPath is null ? DateTime.MaxValue : GetCodeOwnersUtcNow().Add(CodeOwnersLoadFailureRetryDelay));
        var node = _codeOwnersSearchCacheOrder.AddLast(entry);
        _codeOwnersSearchCache.Add(searchCacheKey, node);
    }

    private void RemoveCodeOwnersSearchCacheEntry(LinkedListNode<CodeOwnersSearchCacheEntry> node)
    {
        _codeOwnersSearchCache.Remove(node.Value.Key);
        _codeOwnersSearchCacheOrder.Remove(node);
    }

    private void ClearCodeOwnersSearchCache()
    {
        _codeOwnersSearchCache.Clear();
        _codeOwnersSearchCacheOrder.Clear();
    }

    private DateTime GetCodeOwnersUtcNow() => CodeOwnersUtcNowProvider?.Invoke() ?? DateTime.UtcNow;

    private bool PublishFallbackCodeOwners(string codeOwnersPath, CodeOwners.Platform platform, string root)
    {
        Log.Information("CODEOWNERS file found using fallback search: {Path}", codeOwnersPath);
        return TryPublishCodeOwners(codeOwnersPath, platform, root);
    }

    private bool TryPublishCodeOwners(string codeOwnersPath, CodeOwners.Platform platform, string root)
    {
        if (!CodeOwners.TryLoad(codeOwnersPath, platform, out var parser))
        {
            return false;
        }

        var state = new CodeOwnersState(parser, root, platform);
        Volatile.Write(ref _codeOwnersState, state);
        return true;
    }

    private CodeOwners.Platform GetCodeOwnersPlatform()
        => GetType().Name.Contains("GitlabEnvironmentValues") ? CodeOwners.Platform.GitLab : CodeOwners.Platform.GitHub;

    private readonly struct CodeOwnersFileMetadata : IEquatable<CodeOwnersFileMetadata>
    {
        public CodeOwnersFileMetadata(bool exists, long length, long lastWriteTimeUtcTicks)
        {
            Exists = exists;
            Length = length;
            LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
        }

        public bool Exists { get; }

        public long Length { get; }

        public long LastWriteTimeUtcTicks { get; }

        public bool Equals(CodeOwnersFileMetadata other)
            => Exists == other.Exists &&
               Length == other.Length &&
               LastWriteTimeUtcTicks == other.LastWriteTimeUtcTicks;

        public override bool Equals(object? obj) => obj is CodeOwnersFileMetadata other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Exists ? 1 : 0;
                hashCode = (hashCode * 397) ^ Length.GetHashCode();
                hashCode = (hashCode * 397) ^ LastWriteTimeUtcTicks.GetHashCode();
                return hashCode;
            }
        }
    }

    private sealed class CodeOwnersSearchCacheEntry
    {
        public CodeOwnersSearchCacheEntry(
            string key,
            string? failedCodeOwnersPath,
            CodeOwnersFileMetadata fileMetadata,
            DateTime retryAfterUtc)
        {
            Key = key;
            FailedCodeOwnersPath = failedCodeOwnersPath;
            FileMetadata = fileMetadata;
            RetryAfterUtc = retryAfterUtc;
        }

        public string Key { get; }

        public string? FailedCodeOwnersPath { get; }

        public CodeOwnersFileMetadata FileMetadata { get; }

        public DateTime RetryAfterUtc { get; }
    }

    private sealed class CodeOwnersState
    {
        public CodeOwnersState(CodeOwners parser, string root, CodeOwners.Platform platform)
        {
            Parser = parser;
            Root = root;
            Platform = platform;
        }

        public CodeOwners Parser { get; }

        public string Root { get; }

        public CodeOwners.Platform Platform { get; }
    }
}
