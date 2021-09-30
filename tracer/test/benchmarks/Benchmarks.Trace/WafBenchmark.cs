using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.DataFormat;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.BenchmarkDotNet;

namespace Benchmarks.Trace
{
    [DatadogExporter]
    [MemoryDiagnoser]
    public class WafBenchmark
    {
        private readonly Waf waf;
        private readonly IContext context;
        private readonly Node data;

        public WafBenchmark()
        {
            waf = Waf.Initialize(null);
            context = waf.CreateContext();
            var queryStringDic = new Dictionary<string, Node>();
            var headersDic = new Dictionary<string, Node>();
            var cookiesDic = new Dictionary<string, Node>();
            var dict = new Dictionary<string, Node>
            {
                { AddressesConstants.RequestMethod, Node.NewString("GET") },
                { AddressesConstants.RequestUriRaw, Node.NewString("http://localhost:54587/") },
                { AddressesConstants.RequestQuery, Node.NewMap(queryStringDic) },
                { AddressesConstants.RequestHeaderNoCookies, Node.NewMap(headersDic) },
                { AddressesConstants.RequestCookies, Node.NewMap(cookiesDic) },
            };
            data = Node.NewMap(dict);
        }

        [Benchmark]
        public void StartFinishSpan()
        {
            context.Run(data);
        }

    }
}
