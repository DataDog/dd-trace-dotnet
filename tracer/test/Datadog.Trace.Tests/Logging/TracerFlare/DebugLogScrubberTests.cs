// <copyright file="DebugLogScrubberTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Tests based on agent-scrubbing implementation
// https://github.com/DataDog/datadog-agent/blob/cfb86cab2e717ed763dfe111822d778c53e58bb3/pkg/util/scrubber/default_test.go

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Logging.TracerFlare;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class DebugLogScrubberTests
{
    [Theory]
    [InlineData("conf.yaml", "conf_scrubbed.yaml")]
    [InlineData("conf_multiline.yaml", "conf_multiline_scrubbed.yaml")]
    [InlineData("datadog.yaml", "datadog_scrubbed.yaml")]
    public void ShouldScrub_Yaml(string sourceFilename, string expectedFilename)
    {
        var source = GetData(sourceFilename);
        var expected = GetData(expectedFilename);

        AssertScrubbed(source, expected);
    }

    [Fact]
    public void ShouldScrub_Json()
    {
        var source = GetData("config.json");
        var expected = GetData("config_scrubbed.json");

        var scrubber = new DebugLogScrubber();
        var actual = scrubber.ScrubString(source);

        // actual should be valid json
        // using system.text.json where available as it's more strict
#if NETCOREAPP3_0_OR_GREATER
        System.Text.Json.JsonDocument.Parse(actual);
#else
        Newtonsoft.Json.Linq.JObject.Parse(actual);
#endif

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void ShouldNotScrub_EmptyYaml(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("api_key: aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "api_key: \"***************************abbbb\"")]
    [InlineData("api_key: AAAAAAAAAAAAAAAAAAAAAAAAAAAABBBB", "api_key: \"***************************ABBBB\"")]
    [InlineData("api_key: \"aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb\"", "api_key: \"***************************abbbb\"")]
    [InlineData("api_key: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb'", "api_key: '***************************abbbb'")]
    [InlineData("api_key: |\n\taaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "api_key: |\n\t***************************abbbb")]
    [InlineData("api_key: >\r\n\taaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "api_key: >\r\n\t***************************abbbb")]
    [InlineData("   api_key:   'aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb'   ", "   api_key:   '***************************abbbb'   ")]
    [InlineData(MultiLineData.ConfigApiKeySource1, MultiLineData.ConfigApiKeyExpected1)]
    public void ShouldScrub_ConfigApiKey(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("container_id: \"b32bd6f9b73ba7ccb64953a04b82b48e29dfafab65fd57ca01d3b94a0e024885\"", "container_id: \"b32bd6f9b73ba7ccb64953a04b82b48e29dfafab65fd57ca01d3b94a0e024885\"")]
    public void ShouldNotScrub_ConfigContainerId(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("app_key: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "app_key: \"***********************************abbbb\"")]
    [InlineData("app_key: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBB", "app_key: \"***********************************ABBBB\"")]
    [InlineData("app_key: \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb\"", "app_key: \"***********************************abbbb\"")]
    [InlineData("app_key: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb'", "app_key: '***********************************abbbb'")]
    [InlineData("app_key: |\n\taaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "app_key: |\n\t***********************************abbbb")]
    [InlineData("app_key: >\r\n\taaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "app_key: >\r\n\t***********************************abbbb")]
    [InlineData("   app_key:   'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb'   ", "   app_key:   '***********************************abbbb'   ")]
    public void ShouldScrub_ConfigAppKey(string source, string expected)
        => AssertScrubbed(source, expected);

    [Fact]
    public void ShouldScrub_ConfigRemoteConfigAppKey()
    {
        const string source = "key: \"DDRCM_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABCDE\"";
        const string expected = "key: \"***********************************ABCDE\"";

        AssertScrubbed(source, expected);
    }

    [Theory]
    [InlineData("Connection dropped : ftp://user:password@host:port", "Connection dropped : ftp://user:********@host:port")]
    [InlineData("random_url_key: http://user:password@host:port", "random_url_key: http://user:********@host:port")]
    [InlineData("random_url_key: http://user:p@ssw0r)@host:port", "random_url_key: http://user:********@host:port")]
    [InlineData("random_url_key: http://user:🔑🔒🔐🔓@host:port", "random_url_key: http://user:********@host:port")]
    [InlineData("random_url_key: http://user:password@host", "random_url_key: http://user:********@host")]
    [InlineData("random_url_key: protocol://user:p@ssw0r)@host:port", "random_url_key: protocol://user:********@host:port")]
    [InlineData("random_url_key: \"http://user:password@host:port\"", "random_url_key: \"http://user:********@host:port\"")]
    [InlineData("random_url_key: 'http://user:password@host:port'", "random_url_key: 'http://user:********@host:port'")]
    [InlineData("random_domain_key: 'user:password@host:port'", "random_domain_key: 'user:********@host:port'")]
    [InlineData("random_url_key: |\n\thttp://user:password@host:port", "random_url_key: |\n\thttp://user:********@host:port")]
    [InlineData("random_url_key: >\r\n  http://user:password@host:port", "random_url_key: >\r\n  http://user:********@host:port")]
    [InlineData("   random_url_key:   'http://user:password@host:port'   ", "   random_url_key:   'http://user:********@host:port'   ")]
    [InlineData("   random_url_key:   'mongodb+s.r-v://user:password@host:port'   ", "   random_url_key:   'mongodb+s.r-v://user:********@host:port'   ")]
    [InlineData("   random_url_key:   'mongodb+srv://user:pass-with-hyphen@abc.example.com/database'   ", "   random_url_key:   'mongodb+srv://user:********@abc.example.com/database'   ")]
    [InlineData("   random_url_key:   'http://user-with-hyphen:pass-with-hyphen@abc.example.com/database'   ", "   random_url_key:   'http://user-with-hyphen:********@abc.example.com/database'   ")]
    [InlineData("   random_url_key:   'http://user-with-hyphen:pass@abc.example.com/database'   ", "   random_url_key:   'http://user-with-hyphen:********@abc.example.com/database'   ")]
    [InlineData(
        """flushing serie: {"metric":"kubeproxy","tags":["image_id":"foobar/foobaz@sha256:e8dabc7d398d25ecc8a3e33e3153e988e79952f8783b81663feb299ca2d0abdd"]}""",
        """flushing serie: {"metric":"kubeproxy","tags":["image_id":"foobar/foobaz@sha256:e8dabc7d398d25ecc8a3e33e3153e988e79952f8783b81663feb299ca2d0abdd"]}""")]
    [InlineData("\"simple.metric:44|g|@1.00000\"", "\"simple.metric:44|g|@1.00000\"")]
    public void ShouldScrub_UrlPassword(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("Error status code 500 : http://dog.tld/api?key=3290abeefc68e1bbe852a25252bad88c", "Error status code 500 : http://dog.tld/api?key=***************************ad88c")]
    [InlineData("hintedAPIKeyReplacer : http://dog.tld/api_key=InvalidLength12345abbbb", "hintedAPIKeyReplacer : http://dog.tld/api_key=***************************abbbb")]
    [InlineData("hintedAPIKeyReplacer : http://dog.tld/apikey=InvalidLength12345abbbb", "hintedAPIKeyReplacer : http://dog.tld/apikey=***************************abbbb")]
    [InlineData("apiKeyReplacer: https://agent-http-intake.logs.datadoghq.com/v1/input/aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "apiKeyReplacer: https://agent-http-intake.logs.datadoghq.com/v1/input/***************************abbbb")]
    public void ShouldScrub_ApiKey(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("hintedAPPKeyReplacer : http://dog.tld/app_key=InvalidLength12345abbbb", "hintedAPPKeyReplacer : http://dog.tld/app_key=***********************************abbbb")]
    [InlineData("hintedAPPKeyReplacer : http://dog.tld/appkey=InvalidLength12345abbbb", "hintedAPPKeyReplacer : http://dog.tld/appkey=***********************************abbbb")]
    [InlineData("hintedAPPKeyReplacer : http://dog.tld/application_key=InvalidLength12345abbbb", "hintedAPPKeyReplacer : http://dog.tld/application_key=***********************************abbbb")]
    [InlineData("appKeyReplacer: http://dog.tld/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb", "appKeyReplacer: http://dog.tld/***********************************abbbb")]
    public void ShouldScrub_AppKey(string source, string expected)
        => AssertScrubbed(source, expected);

    [Fact]
    public void ShouldScrub_DockerSelfInspectApiKey()
    {
        const string source = """
                              "Env": [
                                 "DD_API_KEY=3290abeefc68e1bbe852a25252bad88c",
                                 "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                                 "DOCKER_DD_AGENT=yes",
                                 "AGENT_VERSION=1:6.0",
                                 "DD_AGENT_HOME=/opt/datadog-agent6/"
                              ]
                              """;
        const string expected = """
                                "Env": [
                                   "DD_API_KEY=***************************ad88c",
                                   "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                                   "DOCKER_DD_AGENT=yes",
                                   "AGENT_VERSION=1:6.0",
                                   "DD_AGENT_HOME=/opt/datadog-agent6/"
                                ]
                                """;
        AssertScrubbed(source, expected);
    }

    [Theory]
    [InlineData("mysql_password: password", "mysql_password: \"********\"")]
    [InlineData("mysql_pass: password", "mysql_pass: \"********\"")]
    [InlineData("password_mysql: password", "password_mysql: \"********\"")]
    [InlineData("mysql_password: p@ssw0r)", "mysql_password: \"********\"")]
    [InlineData("mysql_password: 🔑 🔒 🔐 🔓", "mysql_password: \"********\"")]
    [InlineData("mysql_password: \"password\"", "mysql_password: \"********\"")]
    [InlineData("mysql_password: 'password'", "mysql_password: \"********\"")]
    [InlineData("   mysql_password:   'password'   ", "   mysql_password: \"********\"")]
    [InlineData("pwd: 'password'", "pwd: \"********\"")]
    [InlineData("pwd: p@ssw0r", "pwd: \"********\"")]
    [InlineData("cert_key_password: p@ssw0r", "cert_key_password: \"********\"")]
    [InlineData("cert_key_password: 🔑 🔒 🔐 🔓", "cert_key_password: \"********\"")]
    public void ShouldScrub_Password(string source, string expected)
        => AssertScrubbed(source, expected);

    [Theory]
    [InlineData("community_string: password", "community_string: \"********\"")]
    [InlineData("authKey: password", "authKey: \"********\"")]
    [InlineData("privKey: password", "privKey: \"********\"")]
    [InlineData("community_string: p@ssw0r)", "community_string: \"********\"")]
    [InlineData("community_string: 🔑 🔒 🔐 🔓", "community_string: \"********\"")]
    [InlineData("community_string: p@ssw0r", "community_string: \"********\"")]
    [InlineData("community_string: \"password\"", "community_string: \"********\"")]
    [InlineData("   community_string:   'password'   ", "   community_string: \"********\"")]
    [InlineData(MultiLineData.SnmpSource1, MultiLineData.SnmpExpected1)]
    [InlineData(MultiLineData.SnmpSource2, MultiLineData.SnmpExpected2)]
    [InlineData(MultiLineData.SnmpSource3, MultiLineData.SnmpExpected3)]
    [InlineData(MultiLineData.SnmpSource4, MultiLineData.SnmpExpected4)]
    [InlineData(MultiLineData.SnmpSource5, MultiLineData.SnmpExpected5)]
    [InlineData("community: password", "community: \"********\"")]
    [InlineData("authentication_key: password", "authentication_key: \"********\"")]
    [InlineData("privacy_key: password", "privacy_key: \"********\"")]
    public void ShouldScrub_SnmpConfig(string source, string expected)
    {
        AssertScrubbed(source, expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData("    ")]
    public void ShouldScrub_Certificate(string linePrefix)
    {
        var source =
            $"""
             cert_key: >
             {linePrefix}-----BEGIN PRIVATE KEY-----
             {linePrefix}MIICdQIBADANBgkqhkiG9w0BAQEFAASCAl8wggJbAgEAAoGBAOLJKRals8tGoy7K
             {linePrefix}ljG6/hMcoe16W6MPn47Q601ttoFkMoSJZ1Jos6nxn32KXfG6hCiB0bmf1iyZtaMa
             {linePrefix}idae/ceT7ZNGvqcVffpDianq9r08hClhnU8mTojl38fsvHf//yqZNzn1ZUcLsY9e
             {linePrefix}wG6wl7CsbWCafxaw+PfaCB1uWlnhAgMBAAECgYAI+tQgrHEBFIvzl1v5HiFfWlvj
             {linePrefix}DlxAiabUvdsDVtvKJdCGRPaNYc3zZbjd/LOZlbwT6ogGZJjTbUau7acVk3gS8uKl
             {linePrefix}ydWWODSuxVYxY8Poxt9SIksOAk5WmtMgIg2bTltTb8z3AWAT3qZrHth03la5Zbix
             {linePrefix}ynEngzyj1+ND7YwQAQJBAP00t8/1aqub+rfza+Ddd8OYSMARFH22oxgy2W1O+Gwc
             {linePrefix}Y8Gn3z6TkadfhPxFaUPnBPx8wm3mN+XeSB1nf0KCAWECQQDlSc7jQ/Ps5rxcoekB
             {linePrefix}ldB+VmuR8TfcWdrWSOdHUiLyoJoj+Z7yfrf70gONPP9tUnwX6MYdT8YwzHK34aWv
             {linePrefix}8KiBAkBHddlql5jDVgIsaEbJ77cdPJ1Ll4Zw9FqTOcajUuZJnLmKrhYTUxKIaize
             {linePrefix}BbjvsQN3Pr6gxZiBB3rS0aLY4lgBAkApsH3ZfKWBUYK2JQpEq4S5M+VjJ8TMX9oW
             {linePrefix}VDMZGKoaC3F7UQvBc6DoPItAxvJ6YiEGB+Ddu3+Bp+rD3FdP4iYBAkBh17O56A/f
             {linePrefix}QX49RjRCRIT0w4nvZ3ph9gHEe50E4+Ky5CLQNOPLD/RbBXSEzez8cGysVvzDO3DZ
             {linePrefix}/iN4a8gloY3d
             {linePrefix}-----END PRIVATE KEY-----
             """;

        var expected =
            $"""
             cert_key: >
             {linePrefix}********
             """;

        AssertScrubbed(source, expected);
    }

    [Fact]
    public void ShouldScrub_YamlConfigFile()
    {
        const string source =
            """
            dd_url: https://app.datadoghq.com
            api_key: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            proxy: http://user:password@host:port
            password: foo
            auth_token: bar
            auth_token_file_path: /foo/bar/baz
            kubelet_auth_token_path: /foo/bar/kube_token
            # comment to strip
            network_devices:
              snmp_traps:
                community_strings:
                - 'password1'
                - 'password2'
            log_level: info
            """;
        const string expected =
            """
            dd_url: https://app.datadoghq.com
            api_key: "***************************aaaaa"
            proxy: http://user:********@host:port
            password: "********"
            auth_token: "********"
            auth_token_file_path: /foo/bar/baz
            kubelet_auth_token_path: /foo/bar/kube_token
            network_devices:
              snmp_traps:
                community_strings: "********"
            log_level: info
            """;

        AssertScrubbed(source, expected);
    }

    [Theory]
    [InlineData(
        "Bearer 2fe663014abcd1850076f6d68c0355666db98758262870811cace007cd4a62ba",
        "Bearer ***********************************************************a62ba")]
    [InlineData(
        """Error: Get "https://localhost:5001/agent/status": net/http: invalid header field value "Bearer 260a9c065b6426f81b7abae9e6bca9a16f7a842af65c940e89e3417c7aaec82d\n\n" for key Authorization""",
        """Error: Get "https://localhost:5001/agent/status": net/http: invalid header field value "Bearer ***********************************************************ec82d\n\n" for key Authorization""")]
    [InlineData(
        "AuthBearer 2fe663014abcd1850076f6d68c0355666db98758262870811cace007cd4a62ba",
        "AuthBearer 2fe663014abcd1850076f6d68c0355666db98758262870811cace007cd4a62ba")]
    public void ShouldScrub_BearerToken(string source, string expected)
        => AssertScrubbed(source, expected);

    private static string GetData(string filename)
    {
        var thisAssembly = typeof(DebugLogScrubberTests).Assembly;
        var stream = thisAssembly.GetManifestResourceStream($"Datadog.Trace.Tests.Logging.TracerFlare.Sources.{filename}");
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    private static void AssertScrubbed(string source, string expected)
    {
        var scrubber = new DebugLogScrubber();
        var actual = scrubber.ScrubString(source);

        // ignore changes in line endings
        Normalise(actual).Should().Be(Normalise(expected));

        static string Normalise(string val) => val.Replace(Environment.NewLine, "\n");
    }

    private static class MultiLineData
    {
        public const string SnmpSource1 =
            """
            network_devices:
              snmp_traps:
                community_strings:
                    - 'password1'
                    - 'password2'
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpExpected1 =
            """
            network_devices:
              snmp_traps:
                community_strings: "********"
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpSource2 =
            """
            network_devices:
              snmp_traps:
                community_strings: ['password1', 'password2']
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpExpected2 =
            """
            network_devices:
              snmp_traps:
                community_strings: "********"
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpSource3 =
            """
            network_devices:
              snmp_traps:
                community_strings: []
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpExpected3 =
            """
            network_devices:
              snmp_traps:
                community_strings: "********"
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpSource4 =
            """
            network_devices:
              snmp_traps:
                community_strings: [
               'password1',
               'password2']
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpExpected4 =
            """
            network_devices:
              snmp_traps:
                community_strings: "********"
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpSource5 =
            """
            snmp_traps_config:
              community_strings:
              - 'password1'
              - 'password2'
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string SnmpExpected5 =
            """
            snmp_traps_config:
              community_strings: "********"
            other_config: 1
            other_config_with_list: [abc]
            """;

        public const string ConfigApiKeySource1 =
            """
            additional_endpoints:
              "https://app.datadoghq.com":
              - aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb,
              - bbbbbbbbbbbbbbbbbbbbbbbbbbbbaaaa,
              "https://dog.datadoghq.com":
              - aaaaaaaaaaaaaaaaaaaaaaaaaaaabbbb,
              - bbbbbbbbbbbbbbbbbbbbbbbbbbbbaaaa
            """;

        public const string ConfigApiKeyExpected1 =
            """
            additional_endpoints:
              "https://app.datadoghq.com":
              - "***************************abbbb",
              - "***************************baaaa",
              "https://dog.datadoghq.com":
              - "***************************abbbb",
              - "***************************baaaa"
            """;
    }
}
