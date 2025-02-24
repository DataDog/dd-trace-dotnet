using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace DatadogSymbolsServer.Controllers
{
    [ApiController]
    [Route("")]
    public class SymbolsServerController : ControllerBase
    {
        private readonly ILogger<SymbolsServerController> _logger;
        private readonly ISymbolsCache _symbolsCache;

        public SymbolsServerController(ILogger<SymbolsServerController> logger, ISymbolsCache symbolsCache)
        {
            _logger = logger;
            _symbolsCache = symbolsCache;
        }

        // kind can be symbol file or binary file.
        // Did not find yet how it's used by gdb. For now just use it as the download filename
        [HttpGet("/debuginfod/buildid/{guid}/{kind}")]
        public IActionResult Get(string guid, string kind)
        {
            _logger.LogInformation($"Getting file {guid}");
            var symbolFromCache = _symbolsCache.Get(guid, SymbolKind.Linux);
            if (symbolFromCache != null)
            {
                _logger.LogInformation($"Found {guid} in cache.");
                return new FileStreamResult(symbolFromCache, "application/octet-stream")
                {
                    FileDownloadName = kind
                };
            }

            return NotFound();
        }

        [HttpGet("ms/{file}/{guid}/{ignored}")]
        public IActionResult Get(string file, string guid, string ignored)
        {
            var regex = new Regex(@"elf-buildid-(sym-)?(?<guid>[0-9a-f]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var match = regex.Match(guid);
            var (guidd, kind) = match.Success switch
            {
                true => (match.Groups["guid"].Value, SymbolKind.Linux),
                _ => (guid, SymbolKind.Windows)
            };
            _logger.LogInformation($"Getting file {file}");
            var symbolsFile = _symbolsCache.Get(guidd, kind);
            if (symbolsFile != null)
            {
                _logger.LogInformation($"Found {file} in cache.");
                return new FileStreamResult(symbolsFile, "application/octet-stream")
                {
                    FileDownloadName = file
                };
            }

            return NotFound();
        }


        [HttpGet("ingest")]
        public async Task IngestNewVersion(string version, CancellationToken cancellationToken)
        {
            await _symbolsCache.Ingest(version, cancellationToken);
        }
    }
}
