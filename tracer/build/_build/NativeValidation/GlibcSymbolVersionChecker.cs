// <copyright file="GlibcSymbolVersionChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeValidation;

internal static class GlibcSymbolVersionChecker
{
    private const string GlibcVersionPrefix = "GLIBC_";
    private const string GlibcPrivateVersion = "GLIBC_PRIVATE";

    public static ScanResult Scan(
        IEnumerable<NmSymbol> symbols,
        IEnumerable<string> allowedSymbols = null)
    {
        var requirements = new List<Requirement>();
        var privateSymbols = new List<string>();
        var malformedSymbols = new List<string>();
        var allowedSymbolList = allowedSymbols?.ToList();

        foreach (var symbol in symbols)
        {
            if (!symbol.IsUndefined
             || IsAllowed(symbol.VersionedName, allowedSymbolList))
            {
                continue;
            }

            if (string.Equals(symbol.VersionName, GlibcPrivateVersion, StringComparison.Ordinal))
            {
                privateSymbols.Add(symbol.VersionedName);
                continue;
            }

            if (symbol.VersionName is null
             || !symbol.VersionName.StartsWith(GlibcVersionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var versionText = symbol.VersionName.Substring(GlibcVersionPrefix.Length);
            if (!Version.TryParse(versionText, out var version))
            {
                malformedSymbols.Add(symbol.VersionedName);
                continue;
            }

            requirements.Add(new Requirement(symbol.VersionedName, version));
        }

        return new ScanResult(requirements, privateSymbols, malformedSymbols);
    }

    public static Version GetMaximumVersion(ScanResult result)
    {
        Version maximumVersion = null;

        foreach (var requirement in result.Requirements)
        {
            if (maximumVersion is null || requirement.Version.CompareTo(maximumVersion) > 0)
            {
                maximumVersion = requirement.Version;
            }
        }

        return maximumVersion;
    }

    public static void Validate(
        ScanResult result,
        Version maximumAllowedVersion,
        bool isAlpine,
        string filename)
    {
        if (result.PrivateSymbols.Count > 0)
        {
            throw new InvalidOperationException(
                $"{filename} imports GLIBC_PRIVATE symbols, which are not part of glibc's stable ABI:{Environment.NewLine}"
              + FormatSymbols(result.PrivateSymbols));
        }

        if (result.MalformedSymbols.Count > 0)
        {
            throw new InvalidOperationException(
                $"{filename} has GLIBC symbol versions that could not be parsed:{Environment.NewLine}"
              + FormatSymbols(result.MalformedSymbols));
        }

        if (isAlpine)
        {
            if (result.Requirements.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Alpine build of {filename} should not import glibc symbols:{Environment.NewLine}"
                  + FormatSymbols(result.Requirements.Select(x => x.Symbol)));
            }

            return;
        }

        var incompatibleSymbols = result.Requirements
                                        .Where(x => x.Version.CompareTo(maximumAllowedVersion) > 0)
                                        .Select(x => x.Symbol)
                                        .ToList();

        if (incompatibleSymbols.Count > 0)
        {
            throw new InvalidOperationException(
                $"{filename} imports glibc symbols newer than the allowed maximum {maximumAllowedVersion}:{Environment.NewLine}"
              + FormatSymbols(incompatibleSymbols));
        }
    }

    private static bool IsAllowed(string symbol, IReadOnlyCollection<string> allowedSymbols)
    {
        if (allowedSymbols is null)
        {
            return false;
        }

        // Preserve the previous allowlist behavior, which accepts a configured
        // full symbol/version pair appearing within the nm symbol text.
        return allowedSymbols.Any(allowed => symbol.IndexOf(allowed, StringComparison.Ordinal) >= 0);
    }

    private static string FormatSymbols(IEnumerable<string> symbols)
        => string.Join(
            Environment.NewLine,
            symbols.Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(x => $"  {x}"));

    internal sealed class ScanResult
    {
        public ScanResult(
            IReadOnlyList<Requirement> requirements,
            IReadOnlyList<string> privateSymbols,
            IReadOnlyList<string> malformedSymbols)
        {
            Requirements = requirements;
            PrivateSymbols = privateSymbols;
            MalformedSymbols = malformedSymbols;
        }

        public IReadOnlyList<Requirement> Requirements { get; }

        public IReadOnlyList<string> PrivateSymbols { get; }

        public IReadOnlyList<string> MalformedSymbols { get; }
    }

    internal sealed class Requirement
    {
        public Requirement(string symbol, Version version)
        {
            Symbol = symbol;
            Version = version;
        }

        public string Symbol { get; }

        public Version Version { get; }
    }
}
