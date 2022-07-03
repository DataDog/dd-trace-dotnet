// <copyright file="QueryStringObfuscatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http
{
    [Collection(nameof(QueryStringObfuscatorTests))]
    public class QueryStringObfuscatorTests
    {
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(400);

        [Fact]
        public void DoesntObfuscateIfNoPattern()
        {
            var queryStringObfuscator = new QueryStringObfuscator.Obfuscator(null, _timeout);
            var originalQueryString = "key1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2";
            var result = queryStringObfuscator.Obfuscate(originalQueryString);
            result.Should().Be(originalQueryString);
        }

        [Fact]
        public void ObfuscateWithDefaultPattern()
        {
            var queryStringObfuscator = new QueryStringObfuscator.Obfuscator(QueryStringObfuscator.DefaultObfuscationQueryStringRegex, _timeout);
            var queryString = "key1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2";
            var result = queryStringObfuscator.Obfuscate(queryString);
            result.Should().Be("key1=val1&<redacted>&key2=val2");
        }

        [Fact]
        public void ObfuscateWithCustomPattern()
        {
            var queryStringObfuscator = new QueryStringObfuscator.Obfuscator(@"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|authentic\d*)(?:\s*=[^&]+|""\s*:\s*""[^""]+"")|[a-z0-9\._\-]{100,}", _timeout);
            var queryString = "?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&authentic3=val2&authentic55=v";
            var result = queryStringObfuscator.Obfuscate(queryString);
            result.Should().Be("?<redacted>&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&<redacted>&<redacted>");
        }

        [Fact]
        public void ObfuscateTimeout()
        {
            var querystringObfuscator = new Mock<QueryStringObfuscator.Obfuscator>(() => new QueryStringObfuscator.Obfuscator(@"\[(.*?)\]((?:.\s*)*?)\[\/\1\]", _timeout));
            querystringObfuscator.Setup(q => q.Log(null)).Verifiable();
            const string queryString = @"?[tag1]Test's Text Test Text Test Text Test Text.Test Text Test Text Test ""Text Test Text"" Test Text Test Text.Test Text ? Test Text Test Text Test Text Test Text Test Text Test Text.Test Text, Test Text Test Text Test Text Test Text Test Text Test Text Test Text.[/ ta.g1][tag2]Test's Text Test Text Test Text Test Text.Test Text Test Text Test ""Text Test Text"" Test Text Test Text.Test Text ? Test Text Test Text Test Text Test Text Test Text Test Text.Test Text, Test Text Test Text Test Text Test Text Test Text Test Text Test Text.[/ tag2]";
            var result = querystringObfuscator.Object.Obfuscate(queryString);
            result.Should().Be(string.Empty);
            querystringObfuscator.Verify(o => o.Log(null), Times.Once);
        }
    }
}
