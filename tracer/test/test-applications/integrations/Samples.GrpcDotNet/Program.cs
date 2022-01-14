using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Samples.GrpcDotNet;
using Samples.GrpcDotNet.Services;
using IApplicationLifetime = Microsoft.AspNetCore.Hosting.IApplicationLifetime;

#nullable enable

public static class Program
{
    public static int Main(string[] args)
    {
        var host = WebHost
           .CreateDefaultBuilder(args)
           .ConfigureServices(ConfigureServices)
           .Configure(ConfigureApp)
           .ConfigureKestrel(options =>
            {
                // If we're using http, then _must_ listen on Http2 only, as the TLS
                // negotiation is where we would typically negotiate between Http1.1/Http2
                // Without this, you'll get a PROTOCOL_ERROR
                options.ConfigureEndpointDefaults(
                    opts => opts.Protocols = HttpProtocols.Http2);
            })
           .Build();


        var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();

       // Register a callback to run after the app is fully configured
       lifetime.ApplicationStarted.Register(() =>
       {
           ServerAddress = host.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.First();
           AppListening = true;
       });

        host.Run();

        return ExitCode;
    }

    public static bool AppListening { get; private set; }

    public static string? ServerAddress { get; private set; }

    public static int ExitCode { get; set; }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<Worker>();
        services.AddAuthorization();
        services.AddGrpc(
            opts =>
            {
                opts.Interceptors.Add<ServerInterceptor>();
            });
    }

    private static void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(
            endpoints =>
            {
                endpoints.MapGrpcService<GreeterService>();
                endpoints.MapGet("/", context => context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909"));
            });
    }

    public class ServerInterceptor: Interceptor
    {
        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.UnaryServerHandler(request, context, continuation);
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.ClientStreamingServerHandler(requestStream, context, continuation);
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.ServerStreamingServerHandler(request, responseStream, context, continuation);
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
        }

        private static void AddMeta(ServerCallContext context)
        {
            context.ResponseTrailers.Add("server-value1", "some-server-value");
            context.ResponseTrailers.Add("server-value2", "other-server-value");
        }
    }
}
