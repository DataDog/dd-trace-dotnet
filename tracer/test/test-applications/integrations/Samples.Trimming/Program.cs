
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Samples.Trimming;
#endif


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

#if NET8_0_OR_GREATER
builder.Services
       .AddControllers()
       .AddJsonOptions(
            options =>
            {
                options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, MyJsonContext.Default);
            });
builder.Services.ConfigureHttpJsonOptions(
    options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, MyJsonContext.Default);
    });

#else
builder.Services.AddControllers();
#endif

var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

#if NET8_0_OR_GREATER
// this is a hacky way of making sure the controller isn't trimmed in .NET 8
var initialReport = new Samples.Trimming.Controllers.WeatherForecastController().Get();
#endif

await app.StartAsync();

// Need to send an error log, but no easy way to do that on purpose.
if (Environment.GetEnvironmentVariable("SEND_ERROR_LOG") == "1")
{
    SendErrorLog();
}

var url = app.Urls.First();
var client = new HttpClient();
for (var i = 0; i < 10; i++)
{
    var json = await client.GetStringAsync($"{url}/WeatherForecast");
    Console.WriteLine(json);
}

await app.StopAsync();

static void SendErrorLog([CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
{
    // grab the log field from TracerSettings, as it's an easy way to get an instance
    var settingsType = Type.GetType("Datadog.Trace.Configuration.TracerSettings, Datadog.Trace")!;
    var logField = settingsType.GetField("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var logger = logField.GetValue(null);

    var loggerType = Type.GetType("Datadog.Trace.Logging.DatadogSerilogLogger, Datadog.Trace")!;
    var errorMethod = loggerType.GetMethod("Error", [typeof(string), typeof(int), typeof(string)])!;

    errorMethod.Invoke(logger, ["Sending an error log using hacky reflection", sourceLine, sourceFile]);
}

#if NET8_0_OR_GREATER
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IEnumerable<WeatherForecast>))]
internal partial class MyJsonContext : JsonSerializerContext
{
}
#endif
