
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
#if NETFRAMEWORK
using System.Web;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#endif

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent2]
    public class AppSecBodyBenchmark
    {
        private static readonly Security security;
        private readonly ComplexModel complexModel = new()
        {
            Age = 12,
            Gender = "Female",
            Name = "Tata",
            LastName = "Toto",
            Address = new Address
            {
                Number = 12,
                City = new City { Name = "Paris", Country = new Country { Name = "France", Continent = new Continent { Name = "Europe", Planet = new Planet { Name = "Earth" } } } },
                IsHouse = false,
                NameStreet = "lorem ipsum dolor sit amet"
            },
            Address2 = new Address
            {
                Number = 15,
                City = new City
                {
                    Name = "Madrid",
                    Country = new Country
                    {
                        Name = "Spain",
                        Continent = new Continent
                        {
                            Name = "Europe",
                            Planet = new Planet { Name = "Earth" }
                        }
                    }
                },
                IsHouse = true,
                NameStreet = "lorem ipsum dolor sit amet"
            },
            Dogs = new List<Dog> {
                    new Dog { Name = "toto", Dogs = new List<Dog> { new Dog { Name = "titi" }, new Dog { Name = "titi" } } },
                    new Dog { Name = "toto", Dogs = new List<Dog> { new Dog { Name = "tata" }, new Dog { Name = "tata" } } },
                    new Dog { Name = "tata", Dogs = new List<Dog> { new Dog { Name = "titi" }, new Dog { Name = "titi" }, new Dog { Name = "tutu" } } }
                    }
        };
#if NETFRAMEWORK
        private static HttpContext httpContext;
#else                                   
        private static HttpContext httpContext;
#endif


        static AppSecBodyBenchmark()
        {
            var dir = Directory.GetCurrentDirectory();
            while (!Directory.GetDirectories(dir).Any(s=>s.Contains("shared")))
            {
                dir = Directory.GetParent(dir).FullName;
            }
           
            dir = Directory.GetDirectories(dir).First(s=>s.Contains("shared"));
            Environment.SetEnvironmentVariable("DD_APPSEC_ENABLED", "true");
            var path = Path.Combine(dir, "bin", "monitoring-home", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"win-{(Environment.Is64BitOperatingSystem ? "x64" : "x86")}" : string.Empty);
            Environment.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", path);
                       security = Security.Instance;
#if NETFRAMEWORK
            var ms = new MemoryStream();
            using var sw = new StreamWriter(ms);

            httpContext = new HttpContext(new HttpRequest(string.Empty, "http://random.com/benchmarks", string.Empty), new HttpResponse(sw));
#else
            httpContext = new DefaultHttpContext();
#endif

        }

        [Benchmark]
        public void AllCycleSimpleBody() => ExecuteCycle(new { });

        private void ExecuteCycle(object body)
        {
            security.InstrumentationGateway.RaiseBodyAvailable(httpContext, new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow), body);
#if NETFRAMEWORK
            var context = httpContext.Items["waf"] as IContext;
            context?.Dispose();
            httpContext.Items["waf"] = null;
#else
            var context = httpContext.Features.Get<IContext>();
            context?.Dispose();
            httpContext.Features.Set<IContext>(null);
#endif
        }
        
        [Benchmark]
        public void AllCycleMoreComplexBody() => ExecuteCycle(complexModel);

        [Benchmark]
        public void BodyExtractorSimpleBody() => BodyExtractor.Extract(new { });

        [Benchmark]
        public void BodyExtractorMoreComplexBody() => BodyExtractor.Extract(complexModel);
    }

    public class ComplexModel
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public IEnumerable<Dog> Dogs { get; set; }
        public Dog[] OtherDogs { get; set; }
        public Address Address { get; set; }
        public Address Address2 { get; set; }
        public string Gender { get; set; }
    }
    public class Dog
    {
        public string Name { get; set; }

        public IEnumerable<Dog> Dogs { get; set; }

    }

    public class Address
    {
        public string NameStreet { get; set; }
        public int Number { get; set; }
        public bool IsHouse { get; set; }

        public City City { get; set; }
    }

    public class City
    {
        public string Name { get; set; }
        public Country Country { get; set; }

        public Language Language { get; set; }
    }

    public class Language
    {
        public string Name { get; set; } = "Spanish";
    }

    public class Country
    {
        public string Name { get; set; }
        public Continent Continent { get; set; }
    }

    public class Continent
    {
        public string Name { get; set; }
        public Planet Planet { get; set; }

    }

    public class Planet
    {
        public string Name { get; set; } = "Earth";
    }
}
