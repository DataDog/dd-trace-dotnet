using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.Hangfire
{
    public class Program
    {
        private static readonly ActivitySource AdditionalActivitySource = new("AdditionalActivitySource");

        public static async Task Main(string[] args)
        {
            GlobalConfiguration.Configuration
                               .UseSimpleAssemblyNameTypeSerializer()
                               .UseRecommendedSerializerSettings()
                               .UseMemoryStorage();
            
            
            using var localActivity = AdditionalActivitySource.StartActivity(name: "OtelParent");
            using var server = new BackgroundJobServer();
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();
            
        }

        public static async Task Should_Create_Span()
        {
            BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await Task.Delay(1000);
        }

        public static async Task Should_Create_Span_With_Status_Error_When_Job_Failed()
        {
            BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await Task.Delay(1000);
        }
    }
}
