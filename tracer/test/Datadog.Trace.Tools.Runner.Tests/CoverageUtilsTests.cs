// <copyright file="CoverageUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CoverageUtilsTests
{
    public static IEnumerable<object[]> CombineData()
    {
        yield return
        [
            new GlobalCoverageInfo
            {
                Components =
                {
                    new ComponentCoverageInfo("Component1")
                    {
                        Files =
                        {
                            new FileCoverageInfo("File1")
                            {
                                ExecutableBitmap = [0b_0000_1111],
                                ExecutedBitmap = [0b_0000_1000]
                            },
                            new FileCoverageInfo("File2")
                            {
                                ExecutableBitmap = [0b_1111_0000],
                                ExecutedBitmap = [0b_1000_0000]
                            },
                        }
                    },
                }
            },
            new GlobalCoverageInfo
            {
                Components =
                {
                    new ComponentCoverageInfo("Component1")
                    {
                        Files =
                        {
                            new FileCoverageInfo("File1")
                            {
                                ExecutableBitmap = [0b_0000_1111],
                                ExecutedBitmap = [0b_0000_0111]
                            },
                            new FileCoverageInfo("File2")
                            {
                                ExecutableBitmap = [0b_1111_0000],
                                ExecutedBitmap = [0b_0111_0000]
                            },
                        }
                    },
                    new ComponentCoverageInfo("Component2")
                    {
                        Files =
                        {
                            new FileCoverageInfo("File1")
                            {
                                ExecutableBitmap = [0b_0000_1111],
                                ExecutedBitmap = [0b_0000_1000]
                            },
                            new FileCoverageInfo("File2")
                            {
                                ExecutableBitmap = [0b_1111_0000],
                                ExecutedBitmap = [0b_1000_0000]
                            },
                        }
                    },
                }
            },
            new GlobalCoverageInfo
            {
                Components =
                {
                    new ComponentCoverageInfo("Component1")
                    {
                        Files =
                        {
                            new FileCoverageInfo("File1")
                            {
                                ExecutableBitmap = [0b_0000_1111],
                                ExecutedBitmap = [0b_0000_1111]
                            },
                            new FileCoverageInfo("File2")
                            {
                                ExecutableBitmap = [0b_1111_0000],
                                ExecutedBitmap = [0b_1111_0000]
                            },
                        }
                    },
                    new ComponentCoverageInfo("Component2")
                    {
                        Files =
                        {
                            new FileCoverageInfo("File1")
                            {
                                ExecutableBitmap = [0b_0000_1111],
                                ExecutedBitmap = [0b_0000_1000]
                            },
                            new FileCoverageInfo("File2")
                            {
                                ExecutableBitmap = [0b_1111_0000],
                                ExecutedBitmap = [0b_1000_0000]
                            },
                        }
                    },
                }
            },
        ];
    }

    [Theory]
    [MemberData(nameof(CombineData))]
    internal void CoverageCombineTest(GlobalCoverageInfo a, GlobalCoverageInfo b, GlobalCoverageInfo expected)
    {
        var tmpFolder = Path.GetTempPath();
        tmpFolder = Path.Combine(tmpFolder, $"CoverageCombineTest{DateTime.UtcNow.ToBinary()}");
        if (!Directory.Exists(tmpFolder))
        {
            Directory.CreateDirectory(tmpFolder);
        }

        var aPath = Path.Combine(tmpFolder, "a.json");
        File.WriteAllText(aPath, JsonConvert.SerializeObject(a));

        var bPath = Path.Combine(tmpFolder, "b.json");
        File.WriteAllText(bPath, JsonConvert.SerializeObject(b));

        var outputFile = Path.GetTempFileName();
        global::CoverageUtils.TryCombineAndGetTotalCoverage(tmpFolder, outputFile, out var actualGlobalCoverageInfo).Should().BeTrue();
        actualGlobalCoverageInfo.Should().BeEquivalentTo(expected);

        var outputContent = File.ReadAllText(outputFile);
        JsonConvert.DeserializeObject<GlobalCoverageInfo>(outputContent).Should().BeEquivalentTo(expected);
    }
}
