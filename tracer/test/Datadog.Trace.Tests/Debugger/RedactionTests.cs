// <copyright file="RedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Snapshots;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class RedactionTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("foobar", false)]
        [InlineData("@-_$", false)]
        [InlineData("password", true)]
        [InlineData("PassWord", true)]
        [InlineData("pass-word", true)]
        [InlineData("_Pass-Word_", true)]
        [InlineData("$pass_worD", true)]
        [InlineData("@passWord@", true)]
        [InlineData(" p@sswOrd ", false)]
        [InlineData("PASSWORD", true)]
        [InlineData("paSS@Word", true)]
        [InlineData("someprefix_password", false)]
        [InlineData("password_suffix", false)]
        [InlineData("some_password_suffix", false)]
        [InlineData("Password!", false)]
        [InlineData("!Password", false)]
        public void RedactedKeywordsTest(string keyword, bool shouldYield)
        {
            Assert.Equal(shouldYield, Redaction.IsRedactedKeyword(keyword));
        }

        [Theory]
        [InlineData("x-api-key", true)]
        [InlineData("x_api_key", true)]
        [InlineData("xapikey", true)]
        [InlineData("XApiKey", true)]
        [InlineData("X_Api-Key", true)]
        [InlineData("x_key", false)]
        public void ShouldRedactKeywordsTest(string keyword, bool shouldRedacted)
        {
            Assert.Equal(shouldRedacted, Redaction.ShouldRedact(keyword, typeof(string), out _));
        }
    }
}
