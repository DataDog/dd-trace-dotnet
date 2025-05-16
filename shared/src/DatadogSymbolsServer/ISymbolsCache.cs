namespace DatadogSymbolsServer
{
    public enum SymbolKind
    {
        Linux,
        Windows
    };

    public interface ISymbolsCache
    {
        Stream? Get(string symbolGuid, SymbolKind kind);
        Task Ingest(string version, CancellationToken token);
        Task Initialize(CancellationToken cancellationToken);
    }
}