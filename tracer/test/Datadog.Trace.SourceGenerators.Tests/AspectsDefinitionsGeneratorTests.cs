// <copyright file="AspectsDefinitionsGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests
{
    public class AspectsDefinitionsGeneratorTests
    {
        [Fact]
        public void DoesNotGenerateDefinitionsIfThereAreNone()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;
public class TestList 
{ 
}
""";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);

            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionForSimpleClass()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization })]
public class TestAspectClass
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(object target, object param1, object param2)
    {
        return string.Concat(target, param1, param2);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Propagation,[])] MyTests.TestAspectClass",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionForDefinedGenerics()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<System.String>)")]
    public static string Concat<T>(System.Collections.Generic.IEnumerable<string> values)
    {
        return string.Concat(target, param1);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[None],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.Collections.Generic.IEnumerable`1<System.String>)\",\"\",[0],[False],[None],Default,[])] Concat(System.Collections.Generic.IEnumerable`1<System.String>)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionForUndefinedGenerics()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static string Concat<T>(System.Collections.Generic.IEnumerable<T> values)
    {
        return string.Concat(target, param1);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[None],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)\",\"\",[0],[False],[None],Default,[])] Concat(System.Collections.Generic.IEnumerable`1<!!0>)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionWithMultipleAspects()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization })]
public class TestAspectClass
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Propagation,[])] MyTests.TestAspectClass",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionForTwoClasses()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization })]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(object target, object param1, object param2)
    {
        return string.Concat(target, param1, param2);
    }
}

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization }, AspectType.Sink, new[] { VulnerabilityType.WeakCipher, VulnerabilityType.WeakHash) }]
public class TestAspectClass2
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(object target, object param1, object param2)
    {
        return string.Concat(target, param1, param2);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Sink,[WeakCipher,WeakHash])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateClassAspectsDefinitionWithAllOptions()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new AspectFilter[]{AspectFilter.StringLiterals_Any,AspectFilter.StringLiterals}, AspectType.Source, VulnerabilityType.WeakCipher, VulnerabilityType.WeakHash)]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
}
[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new AspectFilter[]{AspectFilter.StringLiteral_0,AspectFilter.StringLiteral_1}, AspectType.Sink, VulnerabilityType.WeakCipher)]
public class TestAspectClass2
{ 
    [AspectMethodReplace("System.Text.StringBuilder::.ctor(System.String)", "System.Text.StringBuilder", AspectFilter.StringLiterals_Any)]
    public static object Init(string target)
    {
        return new StringBuilder(target);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringLiteral_0,StringLiteral_1],Sink,[WeakCipher])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.Text.StringBuilder::.ctor(System.String)\",\"System.Text.StringBuilder\",[0],[False],[StringLiterals_Any],Default,[])] Init(System.String)",
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringLiterals_Any,StringLiterals],Source,[WeakCipher,WeakHash])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionWithArrayParam()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String[])", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string[] param1)
    {
        return string.Concat(target, param1);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[None],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String[])\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String[])",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionForTwoClassesRasp()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization })]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(object target, object param1, object param2)
    {
        return string.Concat(target, param1, param2);
    }
}

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new[] { AspectFilter.StringOptimization }, AspectType.RaspIastSink, new[] { VulnerabilityType.WeakCipher, VulnerabilityType.WeakHash) }]
public class TestAspectClass2
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)", AspectFilter.StringLiterals_Any)]
    public static string Concat(object target, object param1, object param2)
    {
        return string.Concat(target, param1, param2);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Sink,[WeakCipher,WeakHash])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
        };

        public static string[] GetRaspAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringOptimization],Sink,[WeakCipher,WeakHash])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
"  [AspectMethodReplace(\"System.String::Concat(System.Object,System.Object,System.Object)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.Object,System.Object,System.Object)",
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateClassAspectsDefinitionWithAllOptionsRasp()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new AspectFilter[]{AspectFilter.StringLiterals_Any,AspectFilter.StringLiterals}, AspectType.Source, VulnerabilityType.WeakCipher, VulnerabilityType.WeakHash)]
public class TestAspectClass1
{ 
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        return string.Concat(target, param1);
    }
}
[AspectClass("mscorlib,netstandard,System.Private.CoreLib", new AspectFilter[]{AspectFilter.StringLiteral_0,AspectFilter.StringLiteral_1}, AspectType.RaspIastSink, VulnerabilityType.WeakCipher)]
public class TestAspectClass2
{ 
    [AspectMethodReplace("System.Text.StringBuilder::.ctor(System.String)", "System.Text.StringBuilder", AspectFilter.StringLiterals_Any)]
    public static object Init(string target)
    {
        return new StringBuilder(target);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringLiteral_0,StringLiteral_1],Sink,[WeakCipher])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.Text.StringBuilder::.ctor(System.String)\",\"System.Text.StringBuilder\",[0],[False],[StringLiterals_Any],Default,[])] Init(System.String)",
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringLiterals_Any,StringLiterals],Source,[WeakCipher,WeakHash])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.String,System.String)\",\"\",[0],[False],[StringLiterals_Any],Default,[])] Concat(System.String,System.String)",
        };

        public static string[] GetRaspAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[StringLiteral_0,StringLiteral_1],Sink,[WeakCipher])] MyTests.TestAspectClass2",
"  [AspectMethodReplace(\"System.Text.StringBuilder::.ctor(System.String)\",\"System.Text.StringBuilder\",[0],[False],[StringLiterals_Any],Default,[])] Init(System.String)",
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateAspectsDefinitionWithVersion()
        {
            const string input = """
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

namespace MyTests;

[AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
public class TestAspectClass1
{ 
    [AspectMethodReplaceFromVersion("3.2.0", "System.String::Concat(System.Collections.Generic.IEnumerable)")]
    public static string Concat(System.Collections.Generic.IEnumerable values)
    {
        return string.Concat(target, param1);
    }
}
""";

            const string expected = Constants.FileHeader + """"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
"[AspectClass(\"mscorlib,netstandard,System.Private.CoreLib\",[None],Propagation,[])] MyTests.TestAspectClass1",
"  [AspectMethodReplace(\"System.String::Concat(System.Collections.Generic.IEnumerable)\",\"\",[0],[False],[None],Default,[]);V3.2.0] Concat(System.Collections.Generic.IEnumerable)",
        };

        public static string[] GetRaspAspects() => new string[] {
        };
    }
}

"""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<AspectsDefinitionsGenerator>(
                SourceHelper.AspectAttributes,
                input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        public static class SourceHelper
        {
            public const string AspectAttributes = """
#pragma warning disable CS8603
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast.Dataflow
{
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
internal abstract class AspectAttribute : Attribute
{
    private static Regex nameSplitter = new Regex(@"(?:([^|]+)\|)?(([^:]+)(?:::[^()]+\(.*\))?)", RegexOptions.Compiled); // 1->Assembly 2->Function 3->Type
    private readonly List<object> parameters = new List<object>();

    public AspectAttribute(string targetMethod, AspectType aspectType = AspectType.DEFAULT, params VulnerabilityType[] vulnerabilityTypes)
        : this(targetMethod, string.Empty, 0, false, aspectType, vulnerabilityTypes)
    {
    }

    public AspectAttribute(string targetMethod, params AspectFilter[] filters)
        : this(targetMethod, string.Empty, 0, false, filters)
    {
    }

    public AspectAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
        : this(targetMethod, targetType, 0, false, filters)
    {
    }

    public AspectAttribute(string targetMethod, string targetType, int paramShift, bool boxParam, AspectType aspectType = AspectType.Propagation, params VulnerabilityType[] vulnerabilityTypes)
        : this(targetMethod, targetType, new int[] { paramShift }, new bool[] { boxParam }, new AspectFilter[] { }, aspectType, vulnerabilityTypes)
    {
    }

    public AspectAttribute(string targetMethod, string targetType, int paramShift, bool boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, params VulnerabilityType[] vulnerabilityTypes)
        : this(targetMethod, targetType, new int[] { paramShift }, new bool[] { boxParam }, filters, aspectType, vulnerabilityTypes)
    {
    }

    public AspectAttribute(string targetMethod, string targetType, int[] paramShift, bool[] boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, params VulnerabilityType[] vulnerabilityTypes)
    {
        if (paramShift == null || paramShift.Length == 0) { paramShift = new int[] { 0 }; }
        if (boxParam == null || boxParam.Length == 0) { boxParam = new bool[] { false }; }
        if (filters == null || filters.Length == 0) { filters = new AspectFilter[] { AspectFilter.None }; }

        if (paramShift.Length > 1 && boxParam.Length == 1)
        {
            boxParam = Enumerable.Repeat(boxParam[0], paramShift.Length).ToArray();
        }

        Debug.Assert(paramShift.Length == boxParam.Length, "paramShift and boxParam must be same len");

        parameters.Add(targetMethod.Quote());
        parameters.Add(targetType.Quote());
        parameters.Add(paramShift);
        parameters.Add(boxParam);
        parameters.Add(filters);
        parameters.Add(aspectType);
        parameters.Add(vulnerabilityTypes ?? new VulnerabilityType[0]));

        var targetMethodMatch = nameSplitter.Match(targetMethod);
        TargetMethodAssemblies = GetAssemblyList(targetMethodMatch.Groups[1].Value);
        TargetMethod = targetMethodMatch.Groups[2].Value;
        TargetMethodType = targetMethodMatch.Groups[3].Value;
        TargetTypeAssemblies = TargetMethodAssemblies;
        TargetType = TargetMethodType;

        if (!string.IsNullOrEmpty(targetType))
        {
            var targetTypeMatch = nameSplitter.Match(targetType);
            TargetTypeAssemblies = GetAssemblyList(targetTypeMatch.Groups[1].Value);
            TargetType = targetTypeMatch.Groups[3].Value;
        }

        AspectType = aspectType;
        VulnerabilityTypes = vulnerabilityTypes ?? new VulnerabilityType[0];
        IsVirtual = (TargetMethodType != TargetType);

        ParamShift = paramShift;
        BoxParam = boxParam;
        Filters = filters;
    }

    // Target method data (base virtual)
    public List<string> TargetMethodAssemblies { get; private set; }

    public string TargetMethodType { get; private set; }

    public string TargetMethod { get; }

    // Final type data
    public List<string> TargetTypeAssemblies { get; private set; }

    public string TargetType { get; private set; }

    public bool IsVirtual { get; }

    public int[] ParamShift { get; } // Number of parameters to move up in stack before inyecting the Aspect

    public bool[] BoxParam { get; } // Box parameter before adding call

    public AspectFilter[] Filters { get; } // Filters applied to aspect insertion

    public AspectType AspectType { get; private set; }

    public VulnerabilityType[] VulnerabilityTypes { get; private set; }

    public override string ToString()
    {
        return string.Format("[{0}({1})]", GetType().Name.Replace("Attribute", string.Empty), string.Join(",", parameters.Select(i => ConvertToString(i)).ToArray()));
    }

    internal static List<string> GetAssemblyList(string expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return new List<string>();
        }

        return expression.Split(',').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList<string>();
    }

    internal static string ConvertToString(object? obj)
    {
        if (obj == null) { return "null"; }
        if (obj is Array arr)
        {
            var res = new StringBuilder("[");
            bool first = true;
            foreach (var e in arr)
            {
                if (!first) { res.Append(","); }
                else { first = false; }
                res.Append(ConvertToString(e));
            }

            res.Append("]");
            return res.ToString();
        }

        return Convert.ToString(obj);
    }
}

internal class AspectClassAttribute : Attribute
{
    private readonly List<object> parameters = new List<object>();

    public AspectClassAttribute()
        : this(string.Empty)
    {
    }

    public AspectClassAttribute(string defaultAssembly)
        : this(defaultAssembly, new AspectFilter[0], AspectType.Propagation)
    {
    }

    public AspectClassAttribute(string defaultAssembly, AspectType defaultAspectType, params VulnerabilityType[] defaultVulnerabilityTypes)
        : this(defaultAssembly, new AspectFilter[0], defaultAspectType, defaultVulnerabilityTypes)
    {
    }

    public AspectClassAttribute(string defaultAssembly, AspectFilter filter, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
        : this(defaultAssembly, new AspectFilter[] { filter }, defaultAspectType, defaultVulnerabilityTypes)
    {
    }

    public AspectClassAttribute(string defaultAssembly, AspectFilter[] filters, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
    {
        if (filters.Length == 0) { filters = new AspectFilter[] { AspectFilter.None }; }

        parameters.Add(defaultAssembly.Quote());
        parameters.Add(filters);
        parameters.Add(defaultAspectType);
        parameters.Add(defaultVulnerabilityTypes ?? new VulnerabilityType[0]));

        DefaultAssembly = AspectAttribute.GetAssemblyList(defaultAssembly);
        Filters = filters;
        DefaultAspectType = defaultAspectType;
        DefaultVulnerabilityTypes = defaultVulnerabilityTypes ?? new VulnerabilityType[0];
    }

    public List<string> DefaultAssembly { get; private set; }

    public AspectFilter[] Filters { get; private set; }

    public AspectType DefaultAspectType { get; private set; }

    public VulnerabilityType[] DefaultVulnerabilityTypes { get; private set; }

    public override string ToString()
    {
        return string.Format("[{0}({1})]", GetType().Name.Replace("Attribute", string.Empty), string.Join(",", parameters.Select(i => AspectAttribute.ConvertToString(i)).ToArray()));
    }
}

internal class AspectCtorReplaceAttribute : AspectAttribute
{
    public AspectCtorReplaceAttribute(string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectCtorReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }

    public AspectCtorReplaceAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, aspectType, vulnerabilityTypes)
    {
    }
}

internal enum AspectFilter
{
    /// <summary> No filter </summary>
    None,

    /// <summary> Common string optimizations </summary>
    StringOptimization,

    /// <summary> Filter if all params are String Literals </summary>
    StringLiterals,

    /// <summary> Filter if any pf the params are String Literals </summary>
    StringLiterals_Any,

    /// <summary> Filter if param0 is String Literal </summary>
    StringLiteral_0,

    /// <summary> Filter if param1 is String Literal </summary>
    StringLiteral_1,
}

internal class AspectMethodInsertAfterAttribute : AspectAttribute
{
    public AspectMethodInsertAfterAttribute(string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectMethodInsertAfterAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, aspectType, vulnerabilityTypes)
    {
    }
}

internal class AspectMethodInsertBeforeAttribute : AspectAttribute
{
    public AspectMethodInsertBeforeAttribute(string targetMethod, int paramShift = 0, bool boxParam = false)
        : base(targetMethod, string.Empty, paramShift, boxParam)
    {
    }

    public AspectMethodInsertBeforeAttribute(string targetMethod, params int[] paramShift)
        : base(targetMethod, string.Empty, paramShift, new bool[0], new AspectFilter[0])
    {
    }

    public AspectMethodInsertBeforeAttribute(string targetMethod, int[] paramShift, bool[] boxParam)
        : base(targetMethod, string.Empty, paramShift, boxParam, new AspectFilter[0])
    {
    }
}

internal class AspectMethodReplaceAttribute : AspectAttribute
{
    public AspectMethodReplaceAttribute(string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
        : base(targetMethod, targetType, filters)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, aspectType, vulnerabilityTypes)
    {
    }
}

internal class AspectMethodReplaceFromVersionAttribute : AspectMethodReplaceAttribute
{
    public AspectMethodReplaceFromVersionAttribute(string version, string targetMethod)
        : base(targetMethod)
    {
    }
}

internal enum AspectType
{
    Default,
    Propagation,
    Sink,
    Source,
    RaspIastSink
}
}

namespace Datadog.Trace.Iast
{
internal enum VulnerabilityType
{
    None = 0,    
    WeakCipher,
    WeakHash,
}
}               
""";
        }
    }
}
