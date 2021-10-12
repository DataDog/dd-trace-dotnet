module Samples.Giraffe.App

open System
open System.IO
open System.Collections
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Runtime: string
        ProcessArchitecture: string
        IsProfilerAttached: bool
        TracerPath: string
        EnvVar: list<string*string>
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "Samples.Giraffe" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
                link [ _rel "stylesheet" 
                       _type "text/css"
                       _href "https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/css/bootstrap.min.css"
                       _integrity "sha384-MCw98/SFnGE8fJT3GXwEOngsV7Zt27NXFoaoApmYm81iuXoPkFOJwJ8ERdknLPMO" 
                       _crossorigin "anonymous" ]
                script [ _src "https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/js/bootstrap.min.js"
                         _integrity "sha384-ChfqqxuZUCnJSK3+MXmPNIyE6ZbWh2IMqE241rYiqJxyMiZ6OW/JmZQ5stwEULTy" 
                         _crossorigin "anonymous" ]
                       []
            ]
            body [] content
        ]

    let partial () =
        h1 [ _class "text-center" ] [ encodedText "Samples.Giraffe" ]

    let index (model : Message) =
        let renderTable items =
            div [ _class "container"] 
                [
                    table [ _class "table table-striped table-hover"]
                        [
                        for (col1, col2) in items do
                            yield
                                tr []
                                    [
                                        td [] [ encodedText col1 ]
                                        td [] [ encodedText col2 ]
                                    ]
                        ]
                ]

        let tagList = 
            [
                div [ _class "container" ] 
                    [
                        partial()
                        renderTable
                            [
                                "Runtime", model.Runtime
                                "ProcessArchitecture", model.ProcessArchitecture
                                "IsProfilerAttached", string model.IsProfilerAttached
                                "TracerPath", model.TracerPath
                            ]
                        div [_class "text-center" ] [ encodedText "Environment Variables:" ]
                        renderTable model.EnvVar
                    ]
            ]

        tagList |> layout

// ---------------------------------
// Web app
// ---------------------------------

let createModel() =
    let nativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace")
    let isProfilerAttached = 
        if nativeMethodsType = null then false
        else 
            try
                let profilerAttachedMethodInfo = nativeMethodsType.GetMethod("IsProfilerAttached")
                profilerAttachedMethodInfo.Invoke(null, null) :?> bool
            with ex ->
                false

    let prefixes = [ "COR_"; "CORECLR_"; "DD_"; "DATADOG_" ]

    let convertPrefixed (d: DictionaryEntry) =
        let key = string d.Key
        if prefixes |> Seq.exists (fun prefix -> key.StartsWith prefix) then Some (key, string d.Value) 
        else None

    let envVars = 
        Environment.GetEnvironmentVariables()
        |> Seq.cast
        |> Seq.choose convertPrefixed
        |> Seq.sortBy fst
        |> List.ofSeq

    {
        Runtime = RuntimeInformation.FrameworkDescription
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
        IsProfilerAttached = isProfilerAttached
        TracerPath = if nativeMethodsType = null then "(none)" else nativeMethodsType.Assembly.Location 
        EnvVar = envVars
    }

let correlationIdentifierHeaderName = "sample.correlation.identifier";

let addCorrelationIdentifierToResponse (ctx: HttpContext) = 
    if ctx.Request.Headers.ContainsKey(correlationIdentifierHeaderName) then
       ctx.Response.Headers.Add(correlationIdentifierHeaderName, ctx.Request.Headers.[correlationIdentifierHeaderName]);

let indexHandler (name: string) =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do addCorrelationIdentifierToResponse ctx
            let model     = createModel()
            let view      = Views.index model
            return! ctx.WriteHtmlViewAsync(view)
         }
    
let delayHandler (seconds: int): HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do addCorrelationIdentifierToResponse ctx
            let model     = createModel()
            let view      = Views.index model
            Thread.Sleep(TimeSpan.FromSeconds(float seconds));
            return! ctx.WriteHtmlViewAsync(view) 
        }
let statusCode (statusCode: int ): HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do addCorrelationIdentifierToResponse ctx
            do ctx.Response.StatusCode <- statusCode
            return! text $"Status code has been set to {statusCode}" next ctx
        }

let badRequest: HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do addCorrelationIdentifierToResponse ctx
            return failwith "This was a bad request."
         }

let simpleTextHandler (message: string): HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do addCorrelationIdentifierToResponse ctx
            return! text message next ctx
        }
    
let shutdown: HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        task {
            do! ctx.Response.WriteAsync("Shutting down");
            let _ = Task.Run(fun _ -> ctx.GetService<IHostApplicationLifetime>().StopApplication())
            return None
        }
    
let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/delay/%i" delayHandler
                routef "/api/delay/%i" delayHandler
                routef "/status-code/%i" statusCode
                route "/bad-request" >=> badRequest
                route "/ping" >=> (simpleTextHandler "pong")
                route "/branch/ping" >=> (simpleTextHandler "pong")
                route "/shutdown" >=> shutdown
                route "/alive-check" >=> (simpleTextHandler "yes")
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0