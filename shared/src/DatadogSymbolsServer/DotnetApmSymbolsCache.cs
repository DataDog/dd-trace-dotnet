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
        private readonly IHttpClientFactory _builder;
        private readonly string _rootPath = AppContext.BaseDirectory;
        private readonly HttpClient _httpClient;

        public DotnetApmSymbolsCache(ILogger<DotnetApmSymbolsCache> logger, IHttpClientFactory clientBuilder)
        {
            _rootPath = Path.Combine(AppContext.BaseDirectory, CacheFolderName);
            Directory.CreateDirectory(_rootPath);
            _logger = logger;
            _builder = clientBuilder;
            _httpClient = _builder.CreateClient();
            _httpClient.BaseAddress = new Uri("https://github.com/");
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
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
            _logger.LogInformation($"Looking into {path}");
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                if (files.Length > 0)
                {
                    return File.Open(Path.Combine(path, files[0]), FileMode.Open, FileAccess.Read);
                }
                if (files.Length > 1)
                {
                    _logger.LogWarning($"There is more that one file in {path}. Skipping.");
                }
            }
            return null;
        }

        public async Task Ingest(string version, CancellationToken token)
        {
            _logger.LogInformation($"Ingesting artifacts for version ${version}");
            await IngestLinuxArtifacts(version, token);
            await IngestWindowsArtifacts(version, token);
        }

        private async Task IngestLinuxArtifacts(string version, CancellationToken cancellationToken)
        {
            using var x = await _httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/linux-native-symbols.tar.gz", cancellationToken);

            if (!x.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Failed due to error {x.StatusCode}");
                return;
            }

            _logger.LogInformation($"Saving in {_rootPath} {version}");
            using var gzip = new GZipStream(x.Content.ReadAsStream(), CompressionMode.Decompress);
            var tmpPath = Path.Combine(Path.GetTempPath(), $"symbol_cache_linux_tmp_{version}");
            Directory.CreateDirectory(tmpPath);
            await TarFile.ExtractToDirectoryAsync(gzip, tmpPath, overwriteFiles: true, cancellationToken);

            foreach (var file in Directory.EnumerateFiles(tmpPath, "*.*", SearchOption.AllDirectories))
            {
                var buildId = GetElfBuildId(file);

                if (string.IsNullOrEmpty(buildId))
                {
                    _logger.LogWarning($"Unable to get guid/build id for {file}");
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
            using var binariesRequest = await _httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/windows-tracer-home.zip", cancellationToken);

            if (!binariesRequest.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Failed due to error {binariesRequest.StatusCode}");
                return;
            }

            var tmpPath = Path.Combine(Path.GetTempPath(), $"symbol_cache_windows_tmp_{version}");
            var binariesPath = Path.Combine(tmpPath, "binaries");


            ExtractTo(binariesPath, binariesRequest.Content.ReadAsStream());

            List<(string RelativePath, Guid Guid, uint Age)> pdbFilesInfo = new();

            foreach (var image in Directory.EnumerateFiles(binariesPath, "*.dll", SearchOption.AllDirectories))
            {
                var parent = Path.GetFileName(Path.GetDirectoryName(image));
                if (string.IsNullOrWhiteSpace(parent))
                {
                    continue;
                }

                var peHeader = new PeNet.PeFile(image);
                if (peHeader != null && peHeader.ImageDebugDirectory != null)
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

            using var symbolsRequest = await _httpClient.GetAsync($"DataDog/dd-trace-dotnet/releases/download/v{version}/windows-native-symbols.zip", cancellationToken);

            ExtractTo(symbolsPath, symbolsRequest.Content.ReadAsStream());

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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting symbols cache");

            var ingestionTasks = new List<Task>();
            foreach (var version in new[] { "3.10.1", "3.8.0", "3.9.1", "3.9.0", "3.6.0", "2.61.0", "2.56.0" })
            {
                _logger.LogInformation($"Downloading v{version}");
                ingestionTasks.Add(Task.Run(async () => await Ingest(version, cancellationToken)));
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

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StartingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StoppedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StoppingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
