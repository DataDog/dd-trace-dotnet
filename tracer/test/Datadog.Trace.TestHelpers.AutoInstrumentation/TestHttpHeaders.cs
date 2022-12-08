// <copyright file="TestHttpHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers;

public class TestHttpHeaders
{
    public const string HeaderName1UpperWithMapping = "DATADOG-HEADER-NAME";
    public const string HeaderTagName1WithMapping = "datadog-header-tag";
    public const string HeaderName2 = "sample.correlation.identifier";
    public const string HeaderName3 = "Server";
    public const string HeaderValue3 = "Kestrel";

    public const string HeaderName1WithMapping = "datadog-header-name";
    public const string HeaderValue1 = "asp-net-core";
    public const string HeaderValue2 = "0000-0000-0000";
}
