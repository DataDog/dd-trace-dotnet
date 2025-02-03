// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.Primitives;

//// uncomment to allow manual test using the process ID
// Console.WriteLine($"pid = {System.Diagnostics.Process.GetCurrentProcess().Id}");
// Console.WriteLine("------------------");
// Console.ReadLine();

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/endpoint", async (HttpContext context, int code, int redir, int req, int res, string output) =>
{
    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    stopwatch.Start();
    Console.WriteLine($"code = {code,4} - redir = {redir,3} | output = {output}");

    await Task.Delay(req);

    // handle redirect
    if (redir > 0)
    {
        context.Response.Redirect(BuildUrl("/endpoint", code, redir - 1, req, res, output), permanent: true);

        stopwatch.Stop();
        Console.WriteLine($"redirect {stopwatch.ElapsedMilliseconds}");
        stopwatch.Reset();
        return;
    }

    context.Response.StatusCode = code; // Set the status code BEFORE flushing the headers
    StringValues contentType = new StringValues("text/plain");
    context.Response.Headers.Append("content-type", contentType);
    await context.Response.Body.FlushAsync();
    stopwatch.Stop();
    Console.WriteLine($"{stopwatch.ElapsedMilliseconds}");
    stopwatch.Reset();

    // Wait again for the specified duration before sending the response content
    stopwatch.Start();
    await Task.Delay(res);
    stopwatch.Stop();
    Console.WriteLine($"{stopwatch.ElapsedMilliseconds}");

    await context.Response.WriteAsync(output); // Write the output
});

app.MapGet("/stop", async (HttpContext context) =>
{
    await context.Response.WriteAsync("Stopping the server...");
    await app.StopAsync();
});

// get from the command line the different /endpoint parameters and the number of iterations to run
Thread runner = new Thread(() =>
{
    // wait for the HTTP server to start
    // TODO: maybe there is an event on the app lifetime that can be used to know when the server is ready
    Thread.Sleep(1000);

    int iterations = 1;
    int code = 200;
    int redirections = 0;
    int requestDuration = 0;
    int responseDuration = 0;
    string output = "success";
    var success = ParseArguments(out iterations, out code, out redirections, out requestDuration, out responseDuration, out output);

    if (success)
    {
        // send the requests
        var client = new System.Net.Http.HttpClient();
        var baseUrl = app.Urls.First();
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var url = BuildUrl($"{baseUrl}/endpoint", code, redirections, requestDuration, responseDuration, output);
            Console.WriteLine(url);
            var response = client.GetAsync(url).Result;
            Console.WriteLine($"status code = {response.StatusCode}");
            var content = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"content = {content}");
            Console.WriteLine();
        }
    }

    // stop the server after all the requests have been sent
    Console.WriteLine("Stopping the server...");
    app.StopAsync().Wait();
});
runner.Start();

app.Run();

static bool ParseArguments(out int iterations, out int code, out int redirections, out int requestDuration, out int responseDuration, out string output)
{
    var args = Environment.GetCommandLineArgs();
    iterations = 1;
    code = 200;
    redirections = 0;
    requestDuration = 0;
    responseDuration = 0;
    output = "success";

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--iterations")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --iterations value...");
                return false;
            }

            iterations = int.Parse(args[i]);
        }
        else if (args[i] == "--code")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --code value...");
                return false;
            }

            code = int.Parse(args[i]);
            if (code <= 0)
            {
                Console.WriteLine($"Invalid --code value = {code}...");
                return false;
            }
        }
        else if (args[i] == "--redir")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --redir value...");
                return false;
            }

            redirections = int.Parse(args[i]);
            if (redirections < 0)
            {
                Console.WriteLine($"Invalid --redir value = {redirections}...");
                return false;
            }
        }
        else if (args[i] == "--req")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --req value...");
                return false;
            }

            requestDuration = int.Parse(args[i]);
            if (requestDuration < 0)
            {
                Console.WriteLine($"Invalid --req value = {requestDuration}...");
                return false;
            }
        }
        else if (args[i] == "--res")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --res value...");
                return false;
            }

            responseDuration = int.Parse(args[i]);
            if (responseDuration < 0)
            {
                Console.WriteLine($"Invalid --res value = {responseDuration}...");
                return false;
            }
        }
        else if (args[i] == "--output")
        {
            i++;
            if (i >= args.Length)
            {
                Console.WriteLine("Missing --output value...");
                return false;
            }

            output = args[i];
        }
    }

    return true;
}

static string BuildUrl(string path, int code, int redir, int req, int res, string output)
{
    return $"{path}?code={code}&redir={redir}&req={req}&res={res}&output={output}";
}
