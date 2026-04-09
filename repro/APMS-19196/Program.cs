using ReproApp;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseMiddleware<ReproMiddleware>();

app.MapGet("/", () => "OK");
app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("repro"));
app.MapGet("/throw-unauthorized", (HttpContext _) => throw new UnauthorizedAccessException("repro"));

app.Run();
