// <copyright file="AspNetMvc5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    [Collection("IisTests")]
    public class AspNetMvc5IntegratedWithIast : AspNetMvc5IastTests
    {
        public AspNetMvc5IntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableIast: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5IntegratedWithoutIast : AspNetMvc5IastTests
    {
        public AspNetMvc5IntegratedWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableIast: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5ClassicWithIast : AspNetMvc5IastTests
    {
        public AspNetMvc5ClassicWithIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableIast: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5ClassicWithoutIast : AspNetMvc5IastTests
    {
        public AspNetMvc5ClassicWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableIast: false)
        {
        }
    }

    public abstract class AspNetMvc5IastTests : AspNetBase, IClassFixture<IisFixture>
    {
        private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        private static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        private static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");
        private static readonly Regex HashRegex = new(@"(\S)*""hash"": (-){0,1}([0-9]){1,12},(\r|\n){1,2}      ");

        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _enableIast;

        public AspNetMvc5IastTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableIast)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            EnableIast(enableIast);
            DisableObfuscationQueryString();
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);

            _iisFixture = iisFixture;
            _enableIast = enableIast;
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = "Security." + nameof(AspNetMvc5)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableIast=" + enableIast;
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData(AddressesConstants.RequestQuery, "/Iast/SqlQuery?query=SELECT%20Surname%20from%20Persons%20where%20name%20=%20%27Vicent%27", null)]
        public async Task TestIastSqlInjectionRequest(string test, string url, string body)
        {
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
            var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
            var filename = _enableIast ? "Iast.SqlInjection.AspNetMvc5.IastEnabled" : "Iast.SqlInjection.AspNetMvc5.IastDisabled";
            var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            settings.AddRegexScrubber(HashRegex, string.Empty);
            await VerifyHelper.VerifySpans(spansFiltered, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }

        protected override string GetTestName() => _testName;
    }
}
#endif
