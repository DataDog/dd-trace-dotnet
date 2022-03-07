
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
#if NETFRAMEWORK
using System.Web;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#endif

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class AppSecBodyBenchmark
    {
        private Security security;
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

        [GlobalSetup]
        public void Setup()
        {
            var settings = new SecuritySettings(new NameValueConfigurationSource(new System.Collections.Specialized.NameValueCollection
            { {"DD_APPSEC_ENABLED", "true"} }));
            var ig = new InstrumentationGateway();
            var waf = Waf.Create();
            var cons = typeof(Security).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
            security = cons[0].Invoke(new object[] { settings, ig, waf }) as Security;
            Security.Instance = security;
        }

        [Benchmark]
        public void AllCycleSimpleBody()
        {
#if NETFRAMEWORK
            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms);
            var httpContextMock = new HttpContext(new HttpRequest(string.Empty, string.Empty, string.Empty), new HttpResponse(sw));
            security.InstrumentationGateway.RaiseBodyAvailable(httpContextMock, new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow), new { });
#else
            var httpContextMock = new HttpContextMock(new HttpResponseMock());
            security.InstrumentationGateway.RaiseBodyAvailable(httpContextMock, new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow), new { });
#endif
        }

        [Benchmark]
        public void AllCycleMoreComplexBody()
        {
#if NETFRAMEWORK
            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms);
            var httpContextMock = new HttpContext(new HttpRequest(string.Empty, string.Empty, string.Empty), new HttpResponse(sw));
            security.InstrumentationGateway.RaiseBodyAvailable(httpContextMock, new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow), complexModel);
#else
            var httpContextMock = new HttpContextMock(new HttpResponseMock());
            security.InstrumentationGateway.RaiseBodyAvailable(httpContextMock, new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow), complexModel);
#endif
        }

        [Benchmark]
        public void BodyExtractorSimpleBody() => BodyExtractor.GetKeysAndValues(new { });

        [Benchmark]
        public void BodyExtractorMoreComplexBody() => BodyExtractor.GetKeysAndValues(complexModel);
    }

#if !NETFRAMEWORK
    public class HttpContextMock : HttpContext
    {
        private readonly HttpResponseMock responseMock;

        public HttpContextMock(HttpResponseMock responseMock)
        {
            this.responseMock = responseMock;
        }
        public override ConnectionInfo Connection => throw new NotImplementedException();

        public override IFeatureCollection Features => new FeatureCollection();

        public override IDictionary<object, object> Items { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override HttpRequest Request => throw new NotImplementedException();

        public override CancellationToken RequestAborted { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override IServiceProvider RequestServices { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override HttpResponse Response => responseMock;

        public override ISession Session { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string TraceIdentifier { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override ClaimsPrincipal User { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override WebSocketManager WebSockets => throw new NotImplementedException();

        public override void Abort()
        {
            throw new NotImplementedException();
        }
    }

    public class HttpResponseMock : HttpResponse
    {
        List<Tuple<Func<object, Task>, object>> _onCompletedCallbacks = new();

        public override Stream Body { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override long? ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override IResponseCookies Cookies => throw new NotImplementedException();

        public override bool HasStarted => throw new NotImplementedException();

        public override IHeaderDictionary Headers => throw new NotImplementedException();

        public override HttpContext HttpContext => throw new NotImplementedException();

        public override int StatusCode { get; set; }

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
            _onCompletedCallbacks.Add(new Tuple<Func<object, Task>, object>(callback, state));
        }

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            throw new NotImplementedException();
        }

        public override void Redirect(string location, bool permanent)
        {
            throw new NotImplementedException();
        }


        public async override Task CompleteAsync()
        {
            var callbacks = _onCompletedCallbacks;
            _onCompletedCallbacks = null;
            foreach (var callback in callbacks)
            {
                await callback.Item1(callback.Item2);
            }
        }
    }
#endif

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
