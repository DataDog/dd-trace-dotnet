// <copyright file="RedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast;
using Xunit;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.IAST.Tainted;

public class RedactionTests
{
    private const double _regexTimeout = 0;

    [Theory]
    [InlineData("password")]
    [InlineData("passwd")]
    [InlineData("pwd")]
    [InlineData("pass")]
    [InlineData("pass_phrase")]
    [InlineData("passPhrase")]
    [InlineData("secret")]
    [InlineData("api_key")]
    [InlineData("apikey")]
    [InlineData("secret_key")]
    [InlineData("secretKey")]
    [InlineData("access_key_id")]
    [InlineData("accessKeyId")]
    [InlineData("secret_access_key")]
    [InlineData("secretAccessKey")]
    [InlineData("private_key")]
    [InlineData("privateKey")]
    [InlineData("public_key")]
    [InlineData("publicKey")]
    [InlineData("token")]
    [InlineData("api_token")]
    [InlineData("apiToken")]
    [InlineData("expiration_token")]
    [InlineData("expirationToken")]
    [InlineData("refresh_token")]
    [InlineData("refreshToken")]
    [InlineData("consumer_id")]
    [InlineData("consumerId")]
    [InlineData("consumer_key")]
    [InlineData("consumerKey")]
    [InlineData("consumer_secret")]
    [InlineData("consumerSecret")]
    [InlineData("sign")]
    [InlineData("signature")]
    [InlineData("signed")]
    [InlineData("auth")]
    [InlineData("authorization")]
    [InlineData("authentication")]
    [InlineData("user")]
    public void GivenASensitiveString_WhenProcessedAsKey_ItIsRedacted(string key)
    {
        var redactor = Utils.GetDefaultRedactor(_regexTimeout);
        Assert.True(redactor.IsKeySensitive(key));
    }

    [Theory]
    [InlineData("table")]
    [InlineData("name")]
    [InlineData("count")]
    [InlineData("row")]
    [InlineData("tablename")]
    [InlineData("key")]
    public void GivenANonSensitiveString_WhenProcessedAsKey_ItIsNotRedacted(string key)
    {
        var redactor = Utils.GetDefaultRedactor(_regexTimeout);
        Assert.False(redactor.IsKeySensitive(key));
    }

    [Theory]
    [InlineData("BEARER lwqjedqwdoqwidmoqwndun32i")]
    [InlineData("glpat-xxxxxxxxxxxxxxxxxxxx")]
    [InlineData("""
        >
        -----BEGIN RSA PRIVATE KEY-----
        MIIEpAIBAAKCAQEAkVDOAMenPclQ7z5U3i3QYw4lQuijEyxnEgTXkk88L20moFBU
        4vJkSguvUXrGzNiH+WMWWWTAXBTDdtOHApQJSdU0P4lY+0P3Lw3WeZaetPm583ac
        DlaCk9DaqPQnjpZ/9DLqmx1r5JYAZbCiuXWMA0lzJUOOniwt94BWCnz3+0LbrC7j
        NsiaC7cRc1kmj/Nmu8ydA4eop44tJMlaXb9nnUIxglUm0yL1NDOTzokTP03Fa7JW
        t46gMo6co751nYm43MwOb/cY0Uh6+i59czXuCs0hFpWyEkQJDjcQNXgy9ctI0R/J
        nBbQykSJG8C0cB9nsfwbtuRIQVrgoj65erlXawIDAQABAoIBAByGkTnj93eQilu8
        j6phsfOP9k6RHloIMF+AJdUpyrXApoF344H9dSR38L187YOOyfpxshRwS7aHuOsd
        kPY3my8sNCp4ysfgSqio/b42jAcYsqERWocSAmYD7LiX3SAHeSy1xgoXF3Py4jcU
        Go1vfsGybHEXNurj304jmkBK0d83rYdYFNa58jY+6fCrt7b7SdxcjImvRbx0ByvB
        O/igAQxHLYZAVM+9eD8kHRt6nFkdllGkdynMPx82RllpjyZvxBm8hXeRCXvT78Ja
        9aOx6YZLND6iLinAh2J+zFKTtl+iX8DD+39DMFEgLjgKJB84phux1h/2PP8RS2tp
        5TqWy7ECgYEA+A8HEKKFTaYD4GQaiD+L4gOh2ZcLykdG8IIXRxzCPtv5VWKS2SCz
        WWyFoVRlV4b6q96PJwdS/6skbbWS98HIg3aqhOVaXyGxZHlzRgopE3OfRiDcf/Xd
        bO+Y7phH6h+hMBWpAAojJ+lWzGkg2DewCY0NjkUdOrFAZZWWLrQqGGMCgYEAlfe+
        S3gXGqVk3ZyS4f8TyWrkKfVaRVa2KT0GGBJ8TNOB7xlf0oVmKCKSGoWbY5znt2e2
        OTb6/zL0qzm1R9pNw5tUE5k/cCReZ20TpcHExoc+1prvmoCO8ToYMfGPOTBpRKBo
        Hdtx4xjBVe9omP6c/U8jfMDUL+cEKgvvjHUXv1kCgYEApTo1RYJLcoYjTOLAvYI+
        ZYRv2SSAKPNDME4mvSpNxFr3gEVRdSkP7X+YnvY9LojtDXAIQEHjqgLQF/d69mZw
        bgir2it+/6DMrRUskDmSVK+OJsMavG0DWV1aq4ppVGxPDF1RHYKjGiGVvEBGLV8i
        daornlkw9/g64a86ws8kvusCgYBvnRs7//zyD/aqGUYYfUe0uKFnuPueb5LTzl8i
        u19XrnMeCLyQakhFxrUGmDm2QakTj1TH8GuOU9ZVOXX6LDeERa6lh4D3bZn1T/E3
        hKd3OmFCR73cN6IrVxl60lXOMoGmWdwjnJd+dYYu9yfZ9mXRAX1f9AP4Qu+Oe6Ol
        3d/2wQKBgQCgdA48bkRGFR/OqcGACNVQFcXQYvSabKOZkg303NH7p4pD8Ng6FDW+
        r8r8+M/iMF9q7XvcX5pF8zgGk/MfHOdf9wWv7Uih7CIQzJLEs+OzNqx//Jn1EuV4
        GBudByVPLqUDB5nvcDxTTsDP+gPFQtQ1mAWB1r18s9x4OioqvoV/6Q==
        -----END RSA PRIVATE KEY-----
        """)]
    [InlineData("""
        >
        -----BEGIN OPENSSH PRIVATE KEY-----
        MIIEpAIBAAKCAQEAkVDOAMenPclQ7z5U3i3QYw4lQuijEyxnEgTXkk88L20moFBU
        4vJkSguvUXrGzNiH+WMWWWTAXBTDdtOHApQJSdU0P4lY+0P3Lw3WeZaetPm583ac
        DlaCk9DaqPQnjpZ/9DLqmx1r5JYAZbCiuXWMA0lzJUOOniwt94BWCnz3+0LbrC7j
        NsiaC7cRc1kmj/Nmu8ydA4eop44tJMlaXb9nnUIxglUm0yL1NDOTzokTP03Fa7JW
        t46gMo6co751nYm43MwOb/cY0Uh6+i59czXuCs0hFpWyEkQJDjcQNXgy9ctI0R/J
        nBbQykSJG8C0cB9nsfwbtuRIQVrgoj65erlXawIDAQABAoIBAByGkTnj93eQilu8
        j6phsfOP9k6RHloIMF+AJdUpyrXApoF344H9dSR38L187YOOyfpxshRwS7aHuOsd
        kPY3my8sNCp4ysfgSqio/b42jAcYsqERWocSAmYD7LiX3SAHeSy1xgoXF3Py4jcU
        Go1vfsGybHEXNurj304jmkBK0d83rYdYFNa58jY+6fCrt7b7SdxcjImvRbx0ByvB
        O/igAQxHLYZAVM+9eD8kHRt6nFkdllGkdynMPx82RllpjyZvxBm8hXeRCXvT78Ja
        9aOx6YZLND6iLinAh2J+zFKTtl+iX8DD+39DMFEgLjgKJB84phux1h/2PP8RS2tp
        5TqWy7ECgYEA+A8HEKKFTaYD4GQaiD+L4gOh2ZcLykdG8IIXRxzCPtv5VWKS2SCz
        WWyFoVRlV4b6q96PJwdS/6skbbWS98HIg3aqhOVaXyGxZHlzRgopE3OfRiDcf/Xd
        bO+Y7phH6h+hMBWpAAojJ+lWzGkg2DewCY0NjkUdOrFAZZWWLrQqGGMCgYEAlfe+
        S3gXGqVk3ZyS4f8TyWrkKfVaRVa2KT0GGBJ8TNOB7xlf0oVmKCKSGoWbY5znt2e2
        OTb6/zL0qzm1R9pNw5tUE5k/cCReZ20TpcHExoc+1prvmoCO8ToYMfGPOTBpRKBo
        Hdtx4xjBVe9omP6c/U8jfMDUL+cEKgvvjHUXv1kCgYEApTo1RYJLcoYjTOLAvYI+
        ZYRv2SSAKPNDME4mvSpNxFr3gEVRdSkP7X+YnvY9LojtDXAIQEHjqgLQF/d69mZw
        bgir2it+/6DMrRUskDmSVK+OJsMavG0DWV1aq4ppVGxPDF1RHYKjGiGVvEBGLV8i
        daornlkw9/g64a86ws8kvusCgYBvnRs7//zyD/aqGUYYfUe0uKFnuPueb5LTzl8i
        u19XrnMeCLyQakhFxrUGmDm2QakTj1TH8GuOU9ZVOXX6LDeERa6lh4D3bZn1T/E3
        hKd3OmFCR73cN6IrVxl60lXOMoGmWdwjnJd+dYYu9yfZ9mXRAX1f9AP4Qu+Oe6Ol
        3d/2wQKBgQCgdA48bkRGFR/OqcGACNVQFcXQYvSabKOZkg303NH7p4pD8Ng6FDW+
        r8r8+M/iMF9q7XvcX5pF8zgGk/MfHOdf9wWv7Uih7CIQzJLEs+OzNqx//Jn1EuV4
        GBudByVPLqUDB5nvcDxTTsDP+gPFQtQ1mAWB1r18s9x4OioqvoV/6Q==
        -----END OPENSSH PRIVATE KEY-----
        """)]
    [InlineData("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQCRUM4Ax6c9yVDvPlTeLdBjDiVC6KMTLGcSBNeSTzwvbSagUFTi8mRKC69ResbM2If5YxZZZMBcFMN204cClAlJ1TQ/iVj7Q/cvDdZ5lp60+bnzdpwOVoKT0Nqo9CeOln/0MuqbHWvklgBlsKK5dYwDSXMlQ46eLC33gFYKfPf7QtusLuM2yJoLtxFzWSaP82a7zJ0Dh6inji0kyVpdv2edQjGCVSbTIvU0M5POiRM/TcVrsla3jqAyjpyjvnWdibjczA5v9xjRSHr6Ln1zNe4KzSEWlbISRAkONxA1eDL1y0jRH8mcFtDKRIkbwLRwH2ex/Bu25EhBWuCiPrl6uVdr")]
    public void GivenASensitiveValue_WhenProcessedAsValue_ItIsRedacted(string value)
    {
        var redactor = Utils.GetDefaultRedactor(_regexTimeout);
        Assert.True(redactor.IsValueSensitive(value));
    }
}
