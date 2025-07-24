// <copyright file="CoverageRewriteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Coverage.Collector;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using VerifyTests;
using VerifyXunit;
using Xunit;
