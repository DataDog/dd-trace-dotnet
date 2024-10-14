using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using SecurityCoordinator = Datadog.Trace.AppSec.Coordinator.SecurityCoordinator;
#if NETFRAMEWORK
using System.Web;

#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#endif

namespace Benchmarks.Trace.Asm
{
    [MemoryDiagnoser]
    [BenchmarkAgent7]
    [BenchmarkCategory(Constants.AppSecCategory)]
    [IgnoreProfile]
    public class AppSecBodyBenchmark
    {
        private static readonly Security _security;
        private readonly ComplexModel _complexModel = new()
        {
            Age = 12,
            Gender = "Female",
            Name = "Tata",
            LastName = "Toto",
            Address = new Address { Number = 12, City = new City { Name = "Paris", Country = new Country { Name = "France", Continent = new Continent { Name = "Europe", Planet = new Planet { Name = "Earth" } } } }, IsHouse = false, NameStreet = "lorem ipsum dolor sit amet" },
            Address2 = new Address { Number = 15, City = new City { Name = "Madrid", Country = new Country { Name = "Spain", Continent = new Continent { Name = "Europe", Planet = new Planet { Name = "Earth" } } } }, IsHouse = true, NameStreet = "lorem ipsum dolor sit amet" },
            Dogs = new List<Dog> { new Dog { Name = "toto", Dogs = new List<Dog> { new Dog { Name = "titi" }, new Dog { Name = "titi" } } }, new Dog { Name = "toto", Dogs = new List<Dog> { new Dog { Name = "tata" }, new Dog { Name = "tata" } } }, new Dog { Name = "tata", Dogs = new List<Dog> { new Dog { Name = "titi" }, new Dog { Name = "titi" }, new Dog { Name = "tutu" } } } }
        };

        private readonly Props10String _props10 = ConstructionUtils.ConstructProps10String();
        private readonly Props100String _props100 = ConstructionUtils.ConstructProps100String();
        private readonly Props1000String _props1000 = ConstructionUtils.ConstructProps1000String();

        private readonly Props10Rec _props10x3 = ConstructionUtils.ConstructProps10Rec(3);
        private readonly Props10Rec _props10x6 = ConstructionUtils.ConstructProps10Rec(6);



        private static HttpContext _httpContext;

        static AppSecBodyBenchmark()
        {
            AppSecBenchmarkUtils.SetupDummyAgent();
            var dir = Directory.GetCurrentDirectory();
            Environment.SetEnvironmentVariable("DD_APPSEC_ENABLED", "true");
            _security = Security.Instance;
#if NETFRAMEWORK
            var ms = new MemoryStream();
            using var sw = new StreamWriter(ms);

            _httpContext = new HttpContext(new HttpRequest(string.Empty, "http://random.com/benchmarks", string.Empty), new HttpResponse(sw));
#else
            _httpContext = new DefaultHttpContext();
#endif
        }

        [Benchmark]
        public void AllCycleSimpleBody() => ExecuteCycle(new { });

        private void ExecuteCycle(object body)
        {
            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
#if !NETFRAMEWORK
            _security.CheckBody(_httpContext, span, body, false);
            var context = _httpContext.Features.Get<IContext>();
            context?.Dispose();
            _httpContext.Features.Set<IContext>(null);
#else
            var securityTransport = SecurityCoordinator.Get(_security, span, new SecurityCoordinator.HttpTransport(_httpContext));
            securityTransport!.RunWaf(new Dictionary<string, object> { { AddressesConstants.RequestBody, ObjectExtractor.Extract(body) } });
            var context = _httpContext.Items["waf"] as IContext;
            context?.Dispose();
            _httpContext.Items["waf"] = null;
#endif
        }

        [Benchmark]
        public void AllCycleMoreComplexBody() => ExecuteCycle(_complexModel);

        [Benchmark]
        public void ObjectExtractorSimpleBody() => ObjectExtractor.Extract(new { });

        [Benchmark]
        public void ObjectExtractorMoreComplexBody() => ObjectExtractor.Extract(_complexModel);

        // NOTE: these next eight benchmarks are useful to help understand how the size of an
        // object (graph) affects the ObjectExtractor, but are fail slow, so not worth running
        // in the CI

        public void ObjectExtractorProps10() => ObjectExtractor.Extract(_props10);

        public void ObjectExtractorProps100() => ObjectExtractor.Extract(_props100);

        public void ObjectExtractorProps1000() => ObjectExtractor.Extract(_props1000);

        public void ObjectExtractorProps10x3() => ObjectExtractor.Extract(_props10x3);

        public void ObjectExtractorProps10x6() => ObjectExtractor.Extract(_props10x6);

        public void ObjectExtractorProps10x1000Concurrent() =>
            Parallel.For(0, 999, _ => ObjectExtractor.Extract(_props10));

        public void ObjectExtractorProps100x1000Concurrent() =>
            Parallel.For(0, 999, _ => ObjectExtractor.Extract(_props100));

        public void ObjectExtractorProps1000x1000Concurrent() =>
            Parallel.For(0, 999, _ => ObjectExtractor.Extract(_props1000));
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
