using ReproApp;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// APMS-19196 crash trigger: Complex EH clause patterns that break the sorting algorithm  
app.UseMiddleware<ExtremeEHMiddleware>();   // Contains try-in-handler nesting that triggers InvalidProgramException

app.MapGet("/", () => "OK");
app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("repro"));
app.MapGet("/throw-unauthorized", (HttpContext _) => throw new UnauthorizedAccessException("repro"));
app.MapGet("/throw-argument", (HttpContext _) => throw new ArgumentException("argument repro"));
app.MapGet("/throw-timeout", (HttpContext _) => throw new TimeoutException("timeout repro"));
app.MapGet("/throw-null", (HttpContext _) => throw new NullReferenceException("null repro"));
app.MapGet("/crash", (HttpContext _) => throw new InvalidProgramException("Force crash test"));

app.Run();
