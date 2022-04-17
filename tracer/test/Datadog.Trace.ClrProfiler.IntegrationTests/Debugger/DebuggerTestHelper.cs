// <copyright file="DebuggerTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.PDBs;
using Samples.Probes;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

internal class DebuggerTestHelper
{
    public static ProbeConfiguration CreateProbeDefinition(Type type, string targetFramework)
    {
        const BindingFlags allMask =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;

        var snapshotMethodProbes = type.GetNestedTypes(allMask)
                                       .SelectMany(nestedType => nestedType.GetMethods(allMask))
                                       .Concat(type.GetMethods(allMask))
                                       .Where(
                                            m =>
                                            {
                                                var att = m.GetCustomAttribute<MethodProbeTestDataAttribute>();
                                                return att?.Skip == false && att?.SkipOnFrameworks.Contains(targetFramework) == false;
                                            })
                                       .Select(CreateSnapshotMethodProbe)
                                       .ToArray();

        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att?.Skip == false && att?.SkipOnFrameworks.Contains(targetFramework) == false)
                                     .Select(att => CreateSnapshotLineProbe(type, att))
                                     .ToArray();

        var allProbes = snapshotLineProbes.Concat(snapshotMethodProbes).ToArray();
        if (allProbes.Any())
        {
            return new ProbeConfiguration { Id = Guid.Empty.ToString(), SnapshotProbes = allProbes };
        }

        return null;
    }

    private static SnapshotProbe CreateSnapshotLineProbe(Type type, LineProbeTestDataAttribute line)
    {
        var where = CreateLineProbeWhere(type, line);
        return CreateSnapshotProbe(where);
    }

    private static Where CreateLineProbeWhere(Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogPdbReader.CreatePdbReader(type.Assembly);
        var symbolMethod = reader.ReadMethodSymbolInfo(type.GetMethods().First().MetadataToken);
        var filePath = symbolMethod.SequencePoints.First().Document.URL;
        return new Where() { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
    }

    private static SnapshotProbe CreateSnapshotMethodProbe(MethodInfo method)
    {
        var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
        var where = CreateMethodProbeWhere(method, probeTestData);
        return CreateSnapshotProbe(where);
    }

    private static SnapshotProbe CreateSnapshotProbe(Where where)
    {
        return new SnapshotProbe { Id = Guid.Empty.ToString(), Language = TracerConstants.Language, Active = true, Where = where };
    }

    private static Where CreateMethodProbeWhere(MethodInfo method, MethodProbeTestDataAttribute probeTestData)
    {
        var @where = new Where();
        @where.TypeName = method.DeclaringType.FullName;
        @where.MethodName = method.Name;
        var signature = probeTestData.ReturnTypeName;
        if (probeTestData.ParametersTypeName?.Any() == true)
        {
            signature += "," + string.Join(",", probeTestData.ParametersTypeName);
        }

        @where.Signature = signature;
        return @where;
    }
}
