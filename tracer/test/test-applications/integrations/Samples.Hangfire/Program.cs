using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;

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
                               .UseColouredConsoleLogProvider()
                               .UseMemoryStorage();
            
            
            
            GlobalJobFilters.Filters.Add(new LogEverythingAttribute());
            
            using var localActivity = AdditionalActivitySource.StartActivity(name: "OtelParent");
            Console.WriteLine("before starting server");
            using var server = new BackgroundJobServer();
            Console.WriteLine("after starting server");
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();
            
            Console.ReadLine();
        }

        public static async Task Should_Create_Span()
        {
            Console.WriteLine("before Should_Create_Span");
            BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            Console.WriteLine("after Should_Create_Span");
        }

        public static async Task Should_Create_Span_With_Status_Error_When_Job_Failed()
        {
            BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        }
    }
}
