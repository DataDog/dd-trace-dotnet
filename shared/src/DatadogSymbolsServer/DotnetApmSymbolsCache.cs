using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime;

namespace DatadogSymbolsServer
{
    public class DotnetApmSymbolsCache : ISymbolsCache
    {
        private const string ElfPrefix = "elf";
        private const string CacheFolderName = "symbols_cache";

        private readonly ILogger<DotnetApmSymbolsCache> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _rootPath;

        public DotnetApmSymbolsCache(ILogger<DotnetApmSymbolsCache> logger, IHttpClientFactory clientBuilder, IHostEnvironment environment)
        {
            _rootPath = Path.Combine(environment.ContentRootPath, CacheFolderName);
            Directory.CreateDirectory(_rootPath);
            _logger = logger;
            _clientFactory = clientBuilder;
        }

        public Stream? Get(string guid, SymbolKind kind)
        {
            var fileFolder = kind switch
            {
                SymbolKind.Linux => $"{ElfPrefix}-{guid}",
                SymbolKind.Windows => guid,
                _ => throw new NotImplementedException($"Unknown symbol kind {kind}")
            };

            var path = Path.Combine(_rootPath, fileFolder.ToLower());
            _logger.LogInformation("Looking into {Path}", path);
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                if (files.Length > 0)
                {
                    return File.Open(Path.Combine(path, files[0]), FileMode.Open, FileAccess.Read);
                }
                if (files.Length > 1)
                {
                    _logger.LogWarning("There is more that one file in {Path}. Skipping", path);
                }
            }

            return null;
        }

        public async Task Ingest(string version, CancellationToken token)
        {
            _logger.LogInformation("Ingesting artifacts for version {Version}", version);
            await IngestLinuxArtifacts(version, token);
            await IngestWindowsArtifacts(version, token);
        }

        private async Task IngestLinuxArtifacts(string version, CancellationToken cancellationToken)
        {
            using var httpClient = _clientFactory.CreateClient("github");
            using var x = await httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/linux-native-symbols.tar.gz", cancellationToken);

            if (!x.IsSuccessStatusCode)
            {
                _logger.LogInformation("Failed due to error code {StatusCode}: {Reason}", x.StatusCode, x.ReasonPhrase);
                return;
            }

            _logger.LogInformation("Saving in {Path} {Version}", _rootPath, version);
            var innerStream = await x.Content.ReadAsStreamAsync(cancellationToken);
            await using var gzip = new GZipStream(innerStream, CompressionMode.Decompress);
            var tmpPath = Path.Combine(Path.GetTempPath(), $"symbol_cache_linux_tmp_{version}");
            Directory.CreateDirectory(tmpPath);
            await TarFile.ExtractToDirectoryAsync(gzip, tmpPath, overwriteFiles: true, cancellationToken);

            foreach (var file in Directory.EnumerateFiles(tmpPath, "*.*", SearchOption.AllDirectories))
            {
                var buildId = GetElfBuildId(file);

                if (string.IsNullOrEmpty(buildId))
                {
                    _logger.LogWarning("Unable to get guid/build id for {File}", file);
                    continue;
                }

                // Add elf- as prefix to avoid collision with windows "build id" (guid + age)
                var destFolder = Path.Combine(_rootPath, $"{ElfPrefix}-{buildId}");
                var newfile = Path.Combine(destFolder, Path.GetFileName(file));
                Directory.CreateDirectory(destFolder);
                File.Copy(file, newfile, overwrite: true);
            }

            Directory.Delete(tmpPath, recursive: true);
        }

        private async Task IngestWindowsArtifacts(string version, CancellationToken cancellationToken)
        {
            using var httpClient = _clientFactory.CreateClient("github");

            using var binariesRequest = await httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/windows-tracer-home.zip", cancellationToken);

            if (!binariesRequest.IsSuccessStatusCode)
            {
                _logger.LogInformation("Failed due to error {StatusCode}: {Reason}", binariesRequest.StatusCode, binariesRequest.ReasonPhrase);
                return;
            }

            var tmpPath = Path.Combine(Path.GetTempPath(), $"symbol_cache_windows_tmp_{version}");
            var binariesPath = Path.Combine(tmpPath, "binaries");

            await using var readAsStream = await binariesRequest.Content.ReadAsStreamAsync(cancellationToken);
            ExtractTo(binariesPath, readAsStream);

            List<(string RelativePath, Guid Guid, uint Age)> pdbFilesInfo = [];

            foreach (var image in Directory.EnumerateFiles(binariesPath, "*.dll", SearchOption.AllDirectories))
            {
                var parent = Path.GetFileName(Path.GetDirectoryName(image));
                if (string.IsNullOrWhiteSpace(parent))
                {
                    continue;
                }

                var peHeader = new PeNet.PeFile(image);
                if (peHeader.ImageDebugDirectory != null)
                {
                    foreach (var d in peHeader.ImageDebugDirectory)
                    {
                        if (d != null && d.CvInfoPdb70 != null)
                        {
                            pdbFilesInfo.Add((Path.Combine(parent, GetWindowsFilename(d.CvInfoPdb70.PdbFileName)), d.CvInfoPdb70.Signature, d.CvInfoPdb70.Age));
                        }
                    }
                }
            }

            var symbolsPath = Path.Combine(tmpPath, "symbols");

            using var symbolsRequest = await httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/windows-native-symbols.zip", cancellationToken);

            await using Stream symbolsStream = await symbolsRequest.Content.ReadAsStreamAsync(cancellationToken);
            ExtractTo(symbolsPath, symbolsStream);

            foreach (var fileInfo in pdbFilesInfo)
            {
                var pdbPath = Path.Combine(symbolsPath, fileInfo.RelativePath);
                if (!File.Exists(pdbPath))
                {
                    continue;
                }

                var guidd = fileInfo.Guid.ToString("N").ToLower() + fileInfo.Age.ToString();

                var destFolder = Path.Combine(_rootPath, guidd);
                var newfile = Path.Combine(destFolder, Path.GetFileName(fileInfo.RelativePath));
                Directory.CreateDirectory(destFolder);

                File.Copy(pdbPath, newfile, overwrite: true);
            }

            Directory.Delete(tmpPath, recursive: true);

            void ExtractTo(string destPath, Stream stream)
            {
                using var zip = new ZipArchive(stream);
                Directory.CreateDirectory(destPath);
                zip.ExtractToDirectory(destPath, overwriteFiles: true);
            }

            string GetWindowsFilename(string filePath)
            {
                var i = filePath.LastIndexOf('\\');
                if (i == -1)
                {
                    return filePath;
                }

                return filePath.Substring(i + 1);
            }
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting symbols cache");

            string[] versions = ["3.10.1", "3.8.0", "3.9.1", "3.9.0", "3.6.0", "2.61.0", "2.56.0"];
            var ingestionTasks = new List<Task>(versions.Length);
            foreach (var version in versions)
            {
                _logger.LogInformation("Downloading v{Version}", version);
                ingestionTasks.Add(Ingest(version, cancellationToken));
            }

            await Task.WhenAll(ingestionTasks);

            GC.Collect();
            GC.WaitForFullGCComplete();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForFullGCComplete();
        }

        private string? GetElfBuildId(string file)
        {
            try
            {
                using var elf = ELFReader.Load(file);
                var bid = elf.GetSection(".note.gnu.build-id") as INoteSection;

                if (bid is null)
                {
                    return null;
                }
                return Convert.ToHexString(bid.Description).ToLowerInvariant();

            }
            catch
            {
                _logger.LogWarning($"oops failure getting buildid for file {file}");
            }

            return null;
        }
    }
}
