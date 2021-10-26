// <copyright file="WafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafTests
    {
        private const string FileName = "rule-set.json";

        [Theory]
        [InlineData("args", "[$slice]", "nosqli", "crs-942-290")]
        [InlineData("attack", "appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("key", "<script>", "xss", "crs-941-100")]
        [InlineData("value", "0000012345", "sqli", "crs-942-220")]
        public void QueryStringAttack(string key, string attack, string flow, string rule)
        {
            var args = new Dictionary<string, object>
            {
                {
                    AddressesConstants.RequestMethod, "GET"
                },
                {
                    AddressesConstants.RequestQuery, new Dictionary<string, List<string>>
                    {
                        {
                            key, new List<string>
                            {
                                attack
                            }
                        }
                    }
                },
                {
                    AddressesConstants.RequestUriRaw, "http://localhost:54587/"
                }
            };
            var waf = Waf.Initialize(FileName);
            using var context = waf.CreateContext();
            var result = context.Run(args);
            Assert.Equal(ReturnCode.Monitor, result.ReturnCode);
            var resultData = JsonConvert.DeserializeObject<ResultData[]>(result.Data).FirstOrDefault();
            Assert.Equal(flow, resultData.Flow);
            Assert.Equal(rule, resultData.Rule);
            Assert.Equal("server.request.query", resultData.Filter[0].BindingAccessor);
        }

        [Fact]
        public void UrlRawAttack()
        {
            var args = new Dictionary<string, object>
            {
                {
                    AddressesConstants.RequestMethod, "GET"
                },
                {
                    AddressesConstants.RequestUriRaw, "http://localhost:54587/waf/0x5c0x2e0x2e0x2f"
                }
            };
            var waf = Waf.Initialize(FileName);
            using var context = waf.CreateContext();
            var result = context.Run(args);
            Assert.Equal(ReturnCode.Monitor, result.ReturnCode);
            var resultData = JsonConvert.DeserializeObject<ResultData[]>(result.Data).FirstOrDefault();
            Assert.Equal("lfi", resultData.Flow);
            Assert.Equal("crs-930-100", resultData.Rule);
            Assert.Equal(AddressesConstants.RequestUriRaw, resultData.Filter[0].BindingAccessor);
        }

        [Theory]
        // [InlineData("user-agent", "Arachni/v1", "security_scanner", "ua0-600-12x")]
        [InlineData("x-file-name", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("X-Filename", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("X_Filename", "routing.yml", "command_injection", "crs-932-180")]
        public void HeadersAttack(string header, string content, string flow, string rule)
        {
            var args = new Dictionary<string, object>
            {
                {
                    AddressesConstants.RequestMethod, "GET"
                },
                {
                    AddressesConstants.RequestUriRaw, "http://localhost:54587/"
                },
                {
                    "server.request.headers.no_cookies", new Dictionary<string, string>
                    {
                        {
                            header, content
                        }
                    }
                }
            };
            var waf = Waf.Initialize("rule-set.json");
            using var context = waf.CreateContext();
            var result = context.Run(args);
            Assert.Equal(ReturnCode.Monitor, result.ReturnCode);
            var resultData = JsonConvert.DeserializeObject<ResultData[]>(result.Data).FirstOrDefault();
            Assert.Equal(flow, resultData.Flow);
            Assert.Equal(rule, resultData.Rule);
            Assert.Equal(AddressesConstants.RequestHeadersNoCookies, resultData.Filter[0].BindingAccessor);
        }
    }
}
