using CommandLine;
using Datadog.Trace.TestHelpers;
using MockAgent;
using Xunit.Abstractions;

Options? options = null;
Parser.Default.ParseArguments<Options>(args)
    .WithParsed(o => options = o)
    .WithNotParsed(o => throw new Exception("Error parsing options"));

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(options!);
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();

public class Worker : BackgroundService, ITestOutputHelper
{
    private readonly Options _opts;
    private readonly ILogger<Worker> _logger;
    private readonly List<MockTracerAgent> _agents;
    private readonly TaskCompletionSource _tcs = new();

    public Worker(Options opts, ILogger<Worker> logger)
    {
        _opts = opts;
        _logger = logger;
        _agents = new List<MockTracerAgent>();
    }

    private void DisplayTraces(object? sender, EventArgs<IList<IList<MockSpan>>> args)
    {
        var traces = args.Value;
        foreach (var trace in traces)
        {
            foreach (var span in trace)
            {
                _logger.LogInformation("{Trace}", span.ToString());
            }
        }
    }

    private void DisplayMetrics(object? sender, EventArgs<string> args)
    {
        _logger.LogInformation("Stats: {Stats}", args.Value);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
       stoppingToken.Register(() => _tcs.TrySetCanceled());

       if (_opts.Tcp)
       {
           var agent = MockTracerAgent.Create(this, port: _opts.TracesPort, useStatsd: true, requestedStatsDPort: _opts.MetricsPort, useTelemetry: true);
           _agents.Add(agent);
           _logger.LogInformation("Listening for traces on TCP port {Port}", agent.Port);
           _logger.LogInformation("Listening for traces on UDP port {Port}", agent.StatsdPort);

           if (_opts.ShowTraces)
           {
               agent.RequestDeserialized += DisplayTraces;
           }

           if (_opts.ShowMetrics)
           {
               agent.MetricsReceived += DisplayMetrics;
           }
       }

       if (_opts.UnixDomainSockets)
       {
           // can't enable on windows
           string? metricsUds = null;
           bool useMetrics = !string.IsNullOrEmpty(metricsUds);
           if (Environment.OSVersion.Platform is PlatformID.Unix or PlatformID.MacOSX)
           {
               metricsUds = _opts.MetricsUnixDomainSocketPath;
           }

           var config = new UnixDomainSocketConfig(_opts.TracesUnixDomainSocketPath,  metricsUds)
           {
               UseDogstatsD = useMetrics,
               UseTelemetry = true,
           };

           var agent = MockTracerAgent.Create(this, config);
           _agents.Add(agent);
           _logger.LogInformation("Listening for traces on Unix Domain Socket: {AgentTracesUdsPath}", agent.TracesUdsPath);

           if (useMetrics)
           {
               _logger.LogInformation("Listening for metrics on Unix Domain Socket: {AgentStatsUdsPath}", agent.StatsUdsPath);
           }

           if (_opts.ShowTraces)
           {
               agent.RequestDeserialized += DisplayTraces;
           }

           if (useMetrics && _opts.ShowMetrics)
           {
               agent.MetricsReceived += DisplayMetrics;
           }
       }

       if (_opts.WindowsNamedPipe)
       {
           var config = new WindowsPipesConfig(_opts.TracesPipeName, _opts.MetricsPipeName)
           {
               UseDogstatsD = !string.IsNullOrEmpty(_opts.MetricsPipeName),
               UseTelemetry = true,
           };

           var agent = MockTracerAgent.Create(this, config);
           _agents.Add(agent);
           _logger.LogInformation("Listening for traces on Windows Named Pipe: {AgentTracesWindowsPipeName}", agent.TracesWindowsPipeName);
           _logger.LogInformation("Listening for metrics on Windows Named Pipe: {AgentStatsWindowsPipeName}", agent.StatsWindowsPipeName);

           if (_opts.ShowTraces)
           {
               agent.RequestDeserialized += DisplayTraces;
           }

           if (_opts.ShowMetrics)
           {
               agent.MetricsReceived += DisplayMetrics;
           }
       }

       return _tcs.Task;
    }

    public override void Dispose()
    {
        base.Dispose();
        _logger.LogInformation("Shutting down agents");
        foreach (var agent in _agents)
        {
            agent.Dispose();
        }
    }

    public void WriteLine(string message) => _logger.LogDebug("{Message}", message);

    public void WriteLine(string format, params object[] args) => _logger.LogDebug(format, args);
}
