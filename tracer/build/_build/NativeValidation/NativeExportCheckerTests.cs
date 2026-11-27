// <copyright file="NativeExportCheckerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace NativeValidation;

public class NativeExportCheckerTests
{
    [Fact]
    public void ReportsForbiddenImportAndExportAndIgnoresUnrelatedSymbol()
    {
        // U is an undefined import. T is a defined export. Matching is by name.
        NmSymbol[] symbols =
        [
            new NmSymbol('U', "__libc_single_threaded", "__libc_single_threaded@GLIBC_2.32", "GLIBC_2.32"),
            new NmSymbol('T', "__libc_single_threaded", "__libc_single_threaded", null),
            new NmSymbol('T', "malloc", "malloc@GLIBC_2.17", "GLIBC_2.17"),
        ];

        var found = NativeExportChecker.FindForbiddenSymbols(symbols, new[] { "__libc_single_threaded" });

        found.Should().Equal("__libc_single_threaded", "__libc_single_threaded@GLIBC_2.32");
    }
}
