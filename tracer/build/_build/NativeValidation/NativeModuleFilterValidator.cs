// <copyright file="NativeModuleFilterValidator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;

namespace NativeValidation;

/// <summary>
/// Validates that no assembly shipped in a NuGet package and skipped by dd_profiler_constants.h defines a type that
/// derives from or implements a derived or interface integration target, because the native tracer never instruments
/// types in skipped modules.
///
/// NativeModuleFilterTests applies the same rule to the assemblies shipped with .NET on every PR. The packages on
/// nuget.org change without any change in this repository, so this validation also runs on a schedule.
/// </summary>
public sealed class NativeModuleFilterValidator
{
    // nuget.org search returns at most 1,000 results per request and skips at most 3,000.
    private const int SearchPageSize = 1000;
    private const int MaxSearchResults = 4000;

    // nuget.org reserves the System and Microsoft prefixes for these owners, and search matches whole words of package IDs.
    private static readonly string[] Owners = { "aspnet", "dotnetframework", "Microsoft" };

    private static readonly Regex CallTargetDefinition = new(@"\{\(WCHAR\*\)WStr\(""(?<assembly>[^""]+)""\),\(WCHAR\*\)WStr\(""(?<type>[^""]+)""\),\(WCHAR\*\)WStr\(""[^""]+""\),sig\d+,[^}]*CallTargetKind::(?<kind>\w+)");

    /// <summary>
    /// Scans the latest stable version of each major version of the packages that can contain skipped assemblies.
    /// </summary>
    /// <param name="nativeSourceDirectory">The path to Datadog.Tracer.Native.</param>
    /// <exception cref="Exception">Thrown if a skipped assembly matches a target, or if a package can't be scanned.</exception>
    public async Task Validate(string nativeSourceDirectory)
    {
        var constants = File.ReadAllText(Path.Combine(nativeSourceDirectory, "dd_profiler_constants.h"));
        var skipAssemblies = GetStringArray(constants, "skip_assemblies");
        var skipPrefixes = GetStringArray(constants, "skip_assembly_prefixes");
        var includeAssemblies = GetStringArray(constants, "include_assemblies");

        var targetTypes = CallTargetDefinition.Matches(File.ReadAllText(Path.Combine(nativeSourceDirectory, "Generated", "generated_calltargets.g.cpp")))
                                              .Where(m => m.Groups["kind"].Value != "Default")
                                              .Select(m => $"{m.Groups["assembly"].Value}|{m.Groups["type"].Value}")
                                              .ToHashSet();
        var targetAssemblies = targetTypes.Select(key => key.Substring(0, key.IndexOf('|'))).ToHashSet();

        using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip }) { Timeout = TimeSpan.FromMinutes(10) };
        var services = (await GetJsonAsync(client, "https://api.nuget.org/v3/index.json"))["resources"].AsArray();
        string GetService(string type) => (string)services.First(resource => (string)resource["@type"] == type)["@id"];
        var registrations = GetService("RegistrationsBaseUrl/3.6.0");

        var control = await ScanPackageAsync(client, $"{registrations}system.data.sqlclient/index.json", IsApplicationAssembly, targetTypes, targetAssemblies);
        if (!control.Matches.Any(match => match.Contains("System.Data.SqlClient.SqlCommand -> ")))
        {
            throw new Exception($"{nameof(NativeModuleFilterValidator)} didn't find that System.Data.SqlClient.SqlCommand derives from DbCommand, so it can't find other matches either.");
        }

        var packageIds = await GetPackageIdsAsync(client, GetService("SearchQueryService"), GetService("SearchAutocompleteService"), skipPrefixes.Concat(skipAssemblies).ToList());
        Console.Out.WriteLine($"Scanning {packageIds.Count} packages from nuget.org");

        var scanned = 0;
        var matches = new ConcurrentBag<string>();
        var failures = new ConcurrentBag<string>();
        using var throttle = new SemaphoreSlim(8);

        await Task.WhenAll(packageIds.Select(async id =>
        {
            await throttle.WaitAsync();
            try
            {
                var result = await ScanPackageAsync(client, $"{registrations}{id.ToLowerInvariant()}/index.json", IsSkippedAssembly, targetTypes, targetAssemblies);
                Interlocked.Add(ref scanned, result.Scanned);
                result.Matches.ForEach(matches.Add);
            }
            catch (Exception ex)
            {
                failures.Add($"{id}: {ex.Message}");
            }
            finally
            {
                throttle.Release();
            }
        }));

        if (!failures.IsEmpty)
        {
            Console.Error.WriteLine("The following packages couldn't be scanned:");
            foreach (var failure in failures.OrderBy(x => x))
            {
                Console.Error.WriteLine("  - " + failure);
            }
        }

        if (!matches.IsEmpty)
        {
            Console.Error.WriteLine("The following types derive from or implement an integration target, but the native tracer skips their assemblies:");
            foreach (var match in matches.OrderBy(x => x))
            {
                Console.Error.WriteLine("  - " + match);
            }

            Console.Error.WriteLine("Remove these assemblies from skip_assemblies or skip_assembly_prefixes in dd_profiler_constants.h, or add them to include_assemblies.");
        }

        if (!failures.IsEmpty || !matches.IsEmpty || scanned == 0)
        {
            throw new Exception($"Native module filter validation failed after scanning {scanned} skipped assemblies. See the errors above.");
        }

        Console.Out.WriteLine($"Native module filter validation passed: none of the {scanned} skipped assemblies in {packageIds.Count} packages derive from or implement a derived or interface integration target");

        bool IsSkippedAssembly(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return IsApplicationAssembly(path) &&
                   (skipAssemblies.Contains(name) || (skipPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) && !includeAssemblies.Contains(name)));
        }
    }

    // Analyzers, tools and reference assemblies aren't loaded into applications.
    private static bool IsApplicationAssembly(string path)
        => (path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)) &&
           path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    private static async Task<HashSet<string>> GetPackageIdsAsync(HttpClient client, string searchService, string autocompleteService, List<string> skippedNames)
    {
        // Microsoft.AspNet.* packages ship System.Web.* assemblies.
        var words = skippedNames
                   .Select(name => name.TrimEnd('.').Split('.', ' '))
                   .Select(parts => parts[0] == "Microsoft" && parts.Length > 1 ? parts[1] : parts[0])
                   .Append("AspNet")
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in Owners.SelectMany(owner => words.Select(word => $"owner:{owner} id:{word}")))
        {
            for (var skip = 0; skip < MaxSearchResults; skip += SearchPageSize)
            {
                var results = await GetJsonAsync(client, $"{searchService}?q={Uri.EscapeDataString(query)}&take={SearchPageSize}&skip={skip}&prerelease=false&semVerLevel=2.0.0");
                if ((int)results["totalHits"] > MaxSearchResults)
                {
                    throw new Exception($"nuget.org search can't return all the packages for '{query}'. Split the query in {nameof(NativeModuleFilterValidator)}.");
                }

                var packages = results["data"].AsArray();
                ids.UnionWith(packages.Select(package => (string)package["id"]));
                if (packages.Count < SearchPageSize)
                {
                    break;
                }
            }
        }

        // Packages from other owners that predate the prefix reservation.
        foreach (var name in skippedNames)
        {
            var results = await GetJsonAsync(client, $"{autocompleteService}?q={Uri.EscapeDataString(name)}&take={SearchPageSize}&prerelease=false&semVerLevel=2.0.0");
            ids.UnionWith(results["data"].AsArray().Select(id => (string)id).Where(id => id.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
        }

        return ids;
    }

    private static async Task<(int Scanned, List<string> Matches)> ScanPackageAsync(HttpClient client, string registration, Func<string, bool> selectAssembly, HashSet<string> targetTypes, HashSet<string> targetAssemblies)
    {
        var leaves = new List<JsonNode>();
        foreach (var page in (await GetJsonAsync(client, registration))["items"].AsArray())
        {
            leaves.AddRange((page["items"] ?? (await GetJsonAsync(client, (string)page["@id"]))["items"]).AsArray());
        }

        // Registration leaves are sorted by version. Applications still run older major versions, so scan the latest stable version of each.
        var packages = leaves.Where(leaf => !((string)leaf["catalogEntry"]["version"]).Contains('-'))
                             .GroupBy(leaf => ((string)leaf["catalogEntry"]["version"]).Split('.')[0])
                             .Select(major => major.Last());

        var scanned = 0;
        var matches = new List<string>();
        foreach (var package in packages)
        {
            var catalogEntry = package["catalogEntry"];
            var name = $"{(string)catalogEntry["id"]} {(string)catalogEntry["version"]}";
            var files = (await GetJsonAsync(client, (string)catalogEntry["@id"]))["packageEntries"]?.AsArray().Select(file => (string)file["fullName"]);
            if (files is not null && !files.Any(selectAssembly))
            {
                continue;
            }

            using var archive = new ZipArchive(new MemoryStream(await GetBytesAsync(client, (string)package["packageContent"])), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries.Where(entry => selectAssembly(entry.FullName)))
            {
                using var image = new MemoryStream();
                using (var stream = entry.Open())
                {
                    stream.CopyTo(image);
                }

                image.Position = 0;
                scanned++;
                matches.AddRange(GetTypesMatchingTargets(image, targetTypes, targetAssemblies).Select(match => $"{name} {entry.FullName}: {match}"));
            }
        }

        return (scanned, matches);
    }

    private static List<string> GetTypesMatchingTargets(Stream image, HashSet<string> targetTypes, HashSet<string> targetAssemblies)
    {
        ModuleDefinition module;
        try
        {
            module = ModuleDefinition.ReadModule(image);
        }
        catch (BadImageFormatException)
        {
            // Native binaries ship next to the managed assemblies.
            return new List<string>();
        }

        using (module)
        {
            // The ReJIT preprocessor only matches base types and interfaces in the target assembly itself or in modules that reference it.
            var moduleName = module.Assembly?.Name.Name;
            if (moduleName is null || (!targetAssemblies.Contains(moduleName) && !module.AssemblyReferences.Any(reference => targetAssemblies.Contains(reference.Name))))
            {
                return new List<string>();
            }

            return module.GetTypes()
                         .SelectMany(type => type.Interfaces
                                                 .Select(implementation => implementation.InterfaceType)
                                                 .Append(type.BaseType)
                                                 .Select(parent => GetTypeKey(parent, moduleName))
                                                 .Where(key => key is not null && targetTypes.Contains(key))
                                                 .Select(key => $"{type.FullName} -> {key}"))
                         .ToList();
        }
    }

    private static string GetTypeKey(TypeReference type, string moduleName)
        => type switch
        {
            GenericInstanceType genericInstance => GetTypeKey(genericInstance.ElementType, moduleName),
            TypeDefinition definition => $"{moduleName}|{definition.FullName}",
            { Scope: AssemblyNameReference assembly } => $"{assembly.Name}|{type.FullName}",
            _ => null
        };

    private static async Task<JsonNode> GetJsonAsync(HttpClient client, string url)
        => JsonNode.Parse(await GetBytesAsync(client, url));

    private static async Task<byte[]> GetBytesAsync(HttpClient client, string url)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await client.GetByteArrayAsync(url);
            }
            catch (Exception) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5));
            }
        }
    }

    private static string[] GetStringArray(string source, string name)
    {
        var array = Regex.Match(source, $@"\b{name}(\[\])?\s*=?\s*\{{(?<body>[^}}]*)\}}");
        if (!array.Success)
        {
            throw new Exception($"{name} isn't defined in dd_profiler_constants.h. Update {nameof(NativeModuleFilterValidator)} if it was renamed.");
        }

        return Regex.Matches(array.Groups["body"].Value, @"WStr\(""(?<value>[^""]*)""\)")
                    .Select(m => m.Groups["value"].Value)
                    .ToArray();
    }
}
