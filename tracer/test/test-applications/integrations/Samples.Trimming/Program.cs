var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();

var url = app.Urls.First();
var client = new HttpClient();
for (var i = 0; i < 10; i++)
{
    var json = await client.GetStringAsync($"{url}/WeatherForecast");
    Console.WriteLine(json);
}

await app.StopAsync();
