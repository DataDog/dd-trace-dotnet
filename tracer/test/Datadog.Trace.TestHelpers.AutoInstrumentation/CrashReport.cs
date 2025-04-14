// <copyright file="CrashReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation;

internal class CrashReport : IDisposable
{
    private readonly string _pdbSymbolsFolder;
    private readonly Dictionary<string, List<(ulong Offset, uint Size, string Symbol)>> _pdbSymbols = new();
    private readonly JObject _json;
    private readonly ITestOutputHelper _output;
    private HttpClient _httpClient;

    public CrashReport(string pathToCrashReport, ITestOutputHelper output)
    {
        _output = output;
        _pdbSymbolsFolder = Path.Combine(Path.GetTempPath(), "symbols");
        _json = JObject.Parse(File.ReadAllText(pathToCrashReport));
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public async Task<IReadOnlyList<string>> ResolveCrashStackTrace()
    {
        var result = new List<string>();
        var stackTrace = (JArray)_json["error"]["stack"]["frames"];

        foreach (var frame in stackTrace)
        {
            result.Add(await ResolveSymbol(frame));
        }

        return result;
    }

    private async Task<string> ResolveSymbol(JToken frame)
    {
        var name = frame["function"].ToString();

        if (frame["build_id_type"]?.ToString() == "PDB")
        {
            var buildId = frame["build_id"].ToString();
            var pdbName = $"{Path.GetFileNameWithoutExtension(name)}.pdb";

            var moduleAddress = ulong.Parse(frame["module_base_address"].ToString().Replace("0x", string.Empty), NumberStyles.HexNumber);
            var address = ulong.Parse(frame["ip"].ToString().Replace("0x", string.Empty), NumberStyles.HexNumber);

            var resolvedName = await ResolvePdbSymbol(pdbName, buildId, moduleAddress, address);

            if (resolvedName != null)
            {
                var modulePath = name.Split('!')[0];
                var module = Path.GetFileNameWithoutExtension(modulePath);

                name = $"{module}!{resolvedName}";
            }
        }

        return name;
    }

    private async Task<string> ResolvePdbSymbol(string pdbName, string pdbHash, ulong moduleAddress, ulong address)
    {
        var pdbFolder = Path.Combine(_pdbSymbolsFolder, pdbName, pdbHash);
        var pdbPath = Path.Combine(pdbFolder, pdbName);

        if (!_pdbSymbols.TryGetValue(pdbPath, out var symbols))
        {
            var fileExists = File.Exists(pdbPath);

            if (!fileExists)
            {
                fileExists = await DownloadSymbol(pdbName, pdbHash, pdbFolder);
            }

            if (fileExists)
            {
                using var reader = new SharpPdb.Native.PdbFileReader(pdbPath);

                symbols = reader.Functions
                                .OrderBy(f => f.RelativeVirtualAddress)
                                .Select(f => (f.RelativeVirtualAddress, f.CodeSize, f.GetUndecoratedName()))
                                .ToList();
            }
            else
            {
                symbols = new();
            }

            _pdbSymbols.Add(pdbPath, symbols);
        }

        string name = null;

        foreach (var symbol in symbols)
        {
            if (symbol.Offset > address - moduleAddress)
            {
                break;
            }

            name = $"{symbol.Symbol}+{address - moduleAddress - symbol.Offset:x2}";

            if (symbol.Offset + symbol.Size < address - moduleAddress)
            {
                name = $"<unknown> ({name})";
            }
        }

        return name;
    }

    private async Task<bool> DownloadSymbol(string pdbName, string pdbHash, string destinationFolder)
    {
        var serverUrl = "https://msdl.microsoft.com/download/symbols";
        var url = $"{serverUrl}/{pdbName}/{pdbHash}/{pdbName}";

        _output.WriteLine($"Downloading symbol from {url} to {destinationFolder}");

        _httpClient ??= new HttpClient();

        var response = await _httpClient.GetAsync(url);

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return false;
        }

        var content = await response.Content.ReadAsStreamAsync();

        Directory.CreateDirectory(destinationFolder);

        using var file = File.Create(Path.Combine(destinationFolder, pdbName));
        await content.CopyToAsync(file);

        return true;
    }
}
