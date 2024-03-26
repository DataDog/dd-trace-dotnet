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

var url = app.Urls.First();
var client = new HttpClient();
for (var i = 0; i < 10; i++)
{
    var json = await client.GetStringAsync($"{url}/WeatherForecast");
    Console.WriteLine(json);
}

await app.StopAsync();

#if NET8_0_OR_GREATER
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IEnumerable<WeatherForecast>))]
internal partial class MyJsonContext : JsonSerializerContext
{
}
#endif
