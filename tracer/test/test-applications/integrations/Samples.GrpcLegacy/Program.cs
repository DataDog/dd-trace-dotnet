using System.Threading;
using System.Threading.Tasks;
using Samples.Grpc;

#nullable enable

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        var server = new ServerWorker(new(), new(new()));
        var client = new ClientWorker(new());

        Task serverTask = server.ExecuteAsync(cts.Token);
        await client.ExecuteAsync(cts.Token).ConfigureAwait(false);

        cts.Cancel();

        await serverTask.ConfigureAwait(false);

        return ExitCode;
    }

    public static bool AppListening { get; set; }

    public static int? ServerPort { get; set; }

    public static int ExitCode { get; set; }
}
