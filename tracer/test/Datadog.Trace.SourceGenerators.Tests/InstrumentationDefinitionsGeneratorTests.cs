// <copyright file="InstrumentationDefinitionsGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.InstrumentationDefinitions;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests
{
    public class InstrumentationDefinitionsGeneratorTests
    {
        [Fact]
        public void DoesNotGenerateDefinitionsIfThereAreNone()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    public class TestList 
    { 
    }
}";

            // No tracked steps execute, so have to disable the tracked step assertions
            var (diagnostics, output) = TestHelpers.GetGeneratedTrees<InstrumentationDefinitionsGenerator>([input], assertOutput: false);
            Assert.Empty(output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateIntegrationDefinitionForStandardInstrumentation()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateMultipleIntegrationDefinitionsForStandardInstrumentation()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
    TypeName = ""Confluent.Kafka.Producer`2"",
    MethodName = ""ProduceAsync"",
    ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
    ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
    MinimumVersion = ""1.4.0"",
    MaximumVersion = ""1.*.*"",
    IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceAsyncIntegration
{
}
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ProduceAsync"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Threading.CancellationToken"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateMultipleIntegrationDefinitionWhenHaveMultipleTypes()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
    TypeName = ""Confluent.Kafka.Producer`2"",
    MethodName = ""ProduceAsync"",
    ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
    ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
    MinimumVersion = ""1.4.0"",
    MaximumVersion = ""1.*.*"",
    IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceAsyncIntegration
{
}
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ProduceAsync"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Threading.CancellationToken"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateMultipleIntegrationDefinitionsWhenUsingDerivedIntegrationType()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
    TypeName = ""Confluent.Kafka.Producer`2"",
    MethodName = ""ProduceAsync"",
    ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
    ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
    MinimumVersion = ""1.4.0"",
    MaximumVersion = ""1.*.*"",
    CallTargetIntegrationKind = CallTargetKind.Derived,
    IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceAsyncIntegration
{
}
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ProduceAsync"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Threading.CancellationToken"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"), 1, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateMultipleIntegrationDefinitionsWhenUsingInterfaceIntegrationType()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
    TypeName = ""Confluent.Kafka.Producer`2"",
    MethodName = ""ProduceAsync"",
    ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
    ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
    MinimumVersion = ""1.4.0"",
    MaximumVersion = ""1.*.*"",
    CallTargetIntegrationKind = CallTargetKind.Interface,
    IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceAsyncIntegration
{
}
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ProduceAsync"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Threading.CancellationToken"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"), 2, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceAsyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateMultipleIntegrationDefinitionWithDifferentInstrumentationNames()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}

[InstrumentMethod(
    AssemblyName = ""Confluent.Kafka"",
    TypeName = ""Confluent.Kafka.Producer`2"",
    MethodName = ""ProduceAsync"",
    ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
    ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
    MinimumVersion = ""1.4.0"",
    MaximumVersion = ""1.*.*"",
    IntegrationName = ""MongoDb"")]
public class FakeMongoDbIntegration
{
}
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // Kafka
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Produce"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"), 0, 1),

                // MongoDb
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Confluent.Kafka.Producer`2"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ProduceAsync"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]", "Confluent.Kafka.TopicPartition", "Confluent.Kafka.Message`2[!0,!1]", "System.Threading.CancellationToken"), 4, 1, 4, 0, 1, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.FakeMongoDbIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("Confluent.Kafka,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.KafkaProduceSyncIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.Kafka,
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.FakeMongoDbIntegration"
                    => Datadog.Trace.Configuration.IntegrationId.MongoDb,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateAdoNetIntegration()
        {
            const string input = @"
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

/********************************************************************************
 * MySql
 ********************************************************************************/
#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = ""MySql.Data"",
    TypeName = ""MySql.Data.MySqlClient.MySqlCommand"",
    MinimumVersion = ""6.7.0"",
    MaximumVersion = ""6.*.*"",
    IntegrationName = ""MySql""
    DataReaderType = ""MySql.Data.MySqlClient.MySqlDataReader"",
    DataReaderTaskType = ""System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>"",
    TargetMethodAttributes = new[]
    {
        typeof(CommandExecuteNonQueryAttribute),
        typeof(CommandExecuteReaderAttribute),
    })]
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // MySql
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data.MySqlClient.MySqlCommand"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ExecuteNonQuery"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Int32"), 1, 6, 7, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data.MySqlClient.MySqlCommand"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ExecuteReader"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("MySql.Data.MySqlClient.MySqlDataReader"), 1, 6, 7, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteReaderIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("MySql.Data,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                
                // adonet integrations
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteReaderIntegration"
                    => GetAdoNetIntegrationId(
                        integrationTypeName: integrationTypeName,
                        targetTypeName: targetType.FullName,
                        assemblyName: targetType.Assembly.GetName().Name),
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                { Key: "MySql.Data", Value: "MySql.Data.MySqlClient.MySqlCommand" }
                    or { Key: "MySql.Data", Value: "MySql.Data.MySqlClient.MySqlCommand" }
                    => Datadog.Trace.Configuration.IntegrationId.MySql,
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.AdoNetConstants,
                SourceHelper.AdoNetInstrumentationAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateAdoNetDerivedIntegrations()
        {
            const string input = @"
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

/********************************************************************************
 * MySql
 ********************************************************************************/
#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = ""MySql.Data"",
    TypeName = ""MySql.Data.MySqlClient.MySqlCommand"",
    MinimumVersion = ""6.7.0"",
    MaximumVersion = ""6.*.*"",
    IntegrationName = ""MySql""
    DataReaderType = ""MySql.Data.MySqlClient.MySqlDataReader"",
    DataReaderTaskType = ""System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>"",
    TargetMethodAttributes = new[]
    {
        typeof(CommandExecuteNonQueryAttribute),
        typeof(CommandExecuteReaderAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = ""System.Data"",
    TypeName = ""System.Data.Common.DbCommand"",
    MinimumVersion = ""4.0.0"",
    MaximumVersion = ""4.*.*"",
    IntegrationName = ""AdoNet"",
    DataReaderType = ""System.Data.Common.DbDataReader"",
    DataReaderTaskType = ""System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>"",
    TargetMethodAttributes = new[]
    {
        typeof(CommandExecuteNonQueryDerivedAttribute),
    })]
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // AdoNet
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("System.Data"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("System.Data.Common.DbCommand"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ExecuteNonQuery"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Int32"), 1, 4, 0, 0, 4, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"), 1, 1),

                // MySql
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data.MySqlClient.MySqlCommand"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ExecuteNonQuery"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Int32"), 1, 6, 7, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"), 0, 1),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("MySql.Data.MySqlClient.MySqlCommand"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("ExecuteReader"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("MySql.Data.MySqlClient.MySqlDataReader"), 1, 6, 7, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteReaderIntegration"), 0, 1),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => assemblyName.StartsWith("MySql.Data,", StringComparison.Ordinal);

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                
                // adonet integrations
                "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteNonQueryIntegration"
                    or "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteReaderIntegration"
                    => GetAdoNetIntegrationId(
                        integrationTypeName: integrationTypeName,
                        targetTypeName: targetType.FullName,
                        assemblyName: targetType.Assembly.GetName().Name),
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                { Key: "MySql.Data", Value: "MySql.Data.MySqlClient.MySqlCommand" }
                    or { Key: "MySql.Data", Value: "MySql.Data.MySqlClient.MySqlCommand" }
                    => Datadog.Trace.Configuration.IntegrationId.MySql,
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.AdoNetConstants,
                SourceHelper.AdoNetInstrumentationAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateIntegrationDefinitionForAppSec()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AspNetCore
{
    /// <summary>
    /// setModel calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = ""Microsoft.AspNetCore.Mvc.Core"",
    TypeName = ""Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"",
    MethodName = ""set_Result"",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ""Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"" },
    MinimumVersion = ""2.0.0.0"",
    MaximumVersion = ""6.*.*.*.*"",
    IntegrationName = AspNetCoreConstants.IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
    [InstrumentMethod(
    AssemblyName = ""Microsoft.AspNetCore.Mvc.Core"",
    TypeName = ""Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"",
    MethodName = ""set_Result"",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ""Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"" },
    MinimumVersion = ""2.0.0.0"",
    MaximumVersion = ""6.*.*.*.*"",
    IntegrationName = AspNetCoreConstants.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DefaultModelBindingContext_SetResult_Integration
    {
    }
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // AspNetCore
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.Core"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("set_Result"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"), 2, 2, 0, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"), 0, 2),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.Core"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("set_Result"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"), 2, 2, 0, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"), 1, 2),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => false;

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"
                    or "Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"
                    => Datadog.Trace.Configuration.IntegrationId.AspNetCore,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.AspNetCoreConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void CanGenerateIntegrationDefinitionForMixedCategories()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AspNetCore
{
    /// <summary>
    /// setModel calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = ""Microsoft.AspNetCore.Mvc.Core"",
    TypeName = ""Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"",
    MethodName = ""set_Result"",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ""Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"" },
    MinimumVersion = ""2.0.0.0"",
    MaximumVersion = ""6.*.*.*.*"",
    IntegrationName = AspNetCoreConstants.IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
    [InstrumentMethod(
    AssemblyName = ""Microsoft.AspNetCore.Mvc.Core"",
    TypeName = ""Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"",
    MethodName = ""set_Result"",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ""Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"" },
    MinimumVersion = ""2.0.0.0"",
    MaximumVersion = ""6.*.*.*.*"",
    IntegrationName = AspNetCoreConstants.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.Tracing | InstrumentationCategory.AppSec)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DefaultModelBindingContext_SetResult_Integration
    {
    }
";

            const string expected = Constants.FileHeader + """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class InstrumentationDefinitions
    {
        internal static NativeCallTargetDefinition2[] Instrumentations;

        static InstrumentationDefinitions()
        {
            // CallTarget types
            Instrumentations = new NativeCallTargetDefinition2[]
            {

                // AspNetCore
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.Core"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("set_Result"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"), 2, 2, 0, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"), 0, 6),
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.Core"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("set_Result"), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray("System.Void", "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"), 2, 2, 0, 0, 6, 65535, 65535, NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String("Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"), 1, 3),
            };
        }

        /// <summary>
        /// Checks if the provided <see cref="System.Reflection.Assembly.FullName"/> assembly
        /// is one we instrument. Assumes you have already checked for "well-known" prefixes
        /// like "System" and "Microsoft".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => false;

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {
            return integrationTypeName switch
            {
                // integrations with a single IntegrationId per implementation type
                "Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"
                    or "Datadog.Trace.ClrProfiler.AspNetCore.DefaultModelBindingContext_SetResult_Integration"
                    => Datadog.Trace.Configuration.IntegrationId.AspNetCore,

                // adonet integrations
                _ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                // derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}

""";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.AspNetCoreConstants,
                input);
            Assert.Empty(diagnostics);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void AddsDiagnosticForMissingAssembly()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
    // AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingTypeName()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        // TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingMethodName()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        // MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingReturnTypeName()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        // ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingMinVersion()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        // MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Theory]
        [InlineData("-1.2.0")]
        [InlineData("1..")]
        [InlineData("1.0.")]
        public void AddsDiagnosticForInvalidMinVersionFormat(string minVersion)
        {
            var input = $@"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] {{ KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName }},
        MinimumVersion = ""{minVersion}"",
        MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{{ 
}}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidVersionFormatDiagnostic.Id);
        }

        [Theory]
        [InlineData("-1.2.0")]
        [InlineData("1..")]
        [InlineData("1.0.")]
        public void AddsDiagnosticForInvalidMaxVersionFormat(string maxVersion)
        {
            var input = $@"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] {{ KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName }},
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""{maxVersion}"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{{ 
}}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidVersionFormatDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingMaxVersion()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        // MaximumVersion = ""1.*.*"",
        IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        [Fact]
        public void AddsDiagnosticForMissingIntegrationName()
        {
            const string input = @"
using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[InstrumentMethod(
        AssemblyName = ""Confluent.Kafka"",
        TypeName = ""Confluent.Kafka.Producer`2"",
        MethodName = ""Produce"",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = ""1.4.0"",
        MaximumVersion = ""1.*.*"")]
        // IntegrationName = KafkaConstants.IntegrationName)]
public class KafkaProduceSyncIntegration
{ 
}";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<InstrumentationDefinitionsGenerator>(
                SourceHelper.InstrumentMethodAttribute,
                SourceHelper.ClrNames,
                SourceHelper.KafkaConstants,
                input);
            Assert.Empty(output);
            Assert.Contains(diagnostics, diag => diag.Id == MissingRequiredPropertyDiagnostic.Id);
        }

        public static class SourceHelper
        {
            public const string InstrumentMethodAttribute = @"using Datadog.Trace.ClrProfiler;
using System;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal class InstrumentMethodAttribute : Attribute
{
    public string AssemblyName
    {
        get
        {
            switch (AssemblyNames?.Length ?? 0)
            {
                case 0:
                    return null;
                case 1:
                    return AssemblyNames[0];
                default:
                    throw new InvalidOperationException(""Multiple assemblies are not supported using this property. Use AssemblyNames property instead."");
                    return null;
            }
        }
        set => AssemblyNames = new[] { value };
    }

    public string[] AssemblyNames { get; set; }
    public string TypeName
    {
        get
        {
            switch (TypeNames?.Length ?? 0)
            {
                case 0:
                    return null;
                case 1:
                    return TypeNames[0];
                default:
                    ThrowHelper.ThrowNotSupportedException(""Multiple type names are not supported using this property. Use TypeNames property instead."");
                    return null;
            }
        }
        set => TypeNames = new[] { value };
    }
    public string[] TypeNames { get; set; }
    public string MethodName { get; set; }
    public string ReturnTypeName { get; set; }
    public string[] ParameterTypeNames { get; set; }
    // public IntegrationVersionRange VersionRange { get; } = new IntegrationVersionRange();
    public string MinimumVersion { get; set; }
    public string MaximumVersion { get; set; }
    public string IntegrationName { get; set; }
    public Type CallTargetType { get; set; }
    public CallTargetKind CallTargetIntegrationKind { get; set; } = CallTargetKind.Default;
    public InstrumentationCategory InstrumentationCategory { get; set; };
}

internal enum CallTargetKind
{
    /// <summary>
    /// Default calltarget integration
    /// </summary>
    Default = 0,

    /// <summary>
    /// Derived calltarget integration
    /// </summary>
    Derived = 1,

    /// <summary>
    /// Interface calltarget integration
    /// </summary>
    Interface = 2,
}

[Flags]
internal enum InstrumentationCategory
{
    /// <summary>
    /// Default calltarget integration
    /// </summary>
    Tracing = 1,

    /// <summary>
    /// Derived calltarget integration
    /// </summary>
    AppSec = 2,

    /// <summary>
    /// Derived calltarget integration
    /// </summary>
    Iast = 4
}
";

            public const string KafkaConstants = @"
using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal static class KafkaConstants
{
    internal const string IntegrationName = ""Kafka"";
    internal const string ConsumeOperationName = ""kafka.consume"";
    internal const string ProduceOperationName = ""kafka.produce"";
    internal const string TopicPartitionTypeName = ""Confluent.Kafka.TopicPartition"";
    internal const string MessageTypeName = ""Confluent.Kafka.Message`2[!0,!1]"";
    internal const string ConsumeResultTypeName = ""Confluent.Kafka.ConsumeResult`2[!0,!1]"";
    internal const string ActionOfDeliveryReportTypeName = ""System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"";
    internal const string TaskDeliveryReportTypeName = ""System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"";
    internal const string ServiceName = ""kafka"";
}";

            public const string AspNetCoreConstants = @"
using System;

namespace Datadog.Trace.ClrProfiler.AspNetCore;

internal static class AspNetCoreConstants
{
    internal const string IntegrationName = ""AspNetCore"";
}";

            public const string ClrNames = @"
namespace Datadog.Trace.ClrProfiler
{
    internal static class ClrNames
    {
        public const string Ignore = ""_"";

        public const string Void = ""System.Void"";
        public const string Object = ""System.Object"";
        public const string Bool = ""System.Boolean"";
        public const string String = ""System.String"";

        public const string SByte = ""System.SByte"";
        public const string Byte = ""System.Byte"";

        public const string Int16 = ""System.Int16"";
        public const string Int32 = ""System.Int32"";
        public const string Int64 = ""System.Int64"";

        public const string UInt16 = ""System.UInt16"";
        public const string UInt32 = ""System.UInt32"";
        public const string UInt64 = ""System.UInt64"";

        public const string TimeSpan = ""System.TimeSpan"";

        public const string Stream = ""System.IO.Stream"";

        public const string Task = ""System.Threading.Tasks.Task"";
        public const string CancellationToken = ""System.Threading.CancellationToken"";

        // ReSharper disable once InconsistentNaming
        public const string IAsyncResult = ""System.IAsyncResult"";
        public const string AsyncCallback = ""System.AsyncCallback"";

        public const string HttpRequestMessage = ""System.Net.Http.HttpRequestMessage"";
        public const string HttpResponseMessage = ""System.Net.Http.HttpResponseMessage"";
        public const string HttpResponseMessageTask = ""System.Threading.Tasks.Task`1<System.Net.Http.HttpResponseMessage>"";

        public const string GenericTask = ""System.Threading.Tasks.Task`1"";
        public const string IgnoreGenericTask = ""System.Threading.Tasks.Task`1<_>"";
        public const string GenericParameterTask = ""System.Threading.Tasks.Task`1<T>"";
        public const string ObjectTask = ""System.Threading.Tasks.Task`1<System.Object>"";
        public const string Int32Task = ""System.Threading.Tasks.Task`1<System.Int32>"";

        public const string Type = ""System.Type"";
        public const string ByteArray = ""System.Byte[]"";
    }
}";

            public const string AdoNetConstants = """
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class AdoNetConstants
    {
        public static class TypeNames
        {
            public const string CommandBehavior = "System.Data.CommandBehavior";

            public const string DbDataReaderType = "System.Data.Common.DbDataReader";
            public const string DbDataReaderTaskType = "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>";

            public const string Int32TaskType = "System.Threading.Tasks.Task`1<System.Int32>";
            public const string ObjectTaskType = "System.Threading.Tasks.Task`1<System.Object>";
        }

        public static class MethodNames
        {
            public const string ExecuteNonQuery = "ExecuteNonQuery";
            public const string ExecuteNonQueryAsync = "ExecuteNonQueryAsync";

            public const string ExecuteScalar = "ExecuteScalar";
            public const string ExecuteScalarAsync = "ExecuteScalarAsync";

            public const string ExecuteReader = "ExecuteReader";
            public const string ExecuteReaderAsync = "ExecuteReaderAsync";

            public const string ExecuteDbDataReader = "ExecuteDbDataReader";
            public const string ExecuteDbDataReaderAsync = "ExecuteDbDataReaderAsync";
        }
    }
}
""";

            public const string AdoNetInstrumentationAttribute = """
using System;
using System.ComponentModel;
using System.Data;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// Attribute that indicates that the decorated class is meant to intercept a method
    /// by modifying the method body with callbacks. Used to generate the integration definitions file.
    /// </summary>
    /// <remarks>
    /// Beware that the fullname of this class is being used for App Trimming support in the _build/Build.Steps.cs file
    /// as string. Avoid changing the name and/or namespace of this class.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class AdoNetClientInstrumentMethodsAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the assembly that contains the target method to be intercepted.
        /// Required.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the name of the type that contain the target method to be intercepted.
        /// Required.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the target minimum version.
        /// </summary>
        public string MinimumVersion { get; set; }

        /// <summary>
        /// Gets or sets the target maximum version.
        /// </summary>
        public string MaximumVersion { get; set; }

        /// <summary>
        /// Gets or sets the integration name. Allows to group several integration with a single integration name.
        /// </summary>
        public string IntegrationName { get; set; }

        /// <summary>
        /// Gets or sets the DataReader type to use with target signatures that require it
        /// Required.
        /// </summary>
        public string DataReaderType { get; set; }

        /// <summary>
        /// Gets or sets the DataReader type to use with target signatures that require it
        /// Required.
        /// </summary>
        public string DataReaderTaskType { get; set; }

        /// <summary>
        /// Gets or sets the names of attributes decorated with <see cref="AdoNetTargetSignatureAttribute"/>.
        /// Describes all the signatures to instrument
        /// Required.
        /// </summary>
        public Type[] TargetMethodAttributes { get; set; }

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class AdoNetTargetSignatureAttribute : Attribute
        {
            internal enum AdoNetTargetSignatureReturnType
            {
                /// <summary>
                ///  Uses the fixed return type specified in <see cref="AdoNetTargetSignatureAttribute.ReturnTypeName" />
                /// </summary>
                Default,

                /// <summary>
                ///  Uses the return type specified in <see cref="AdoNetClientInstrumentMethodsAttribute.DataReaderType" />
                /// </summary>
                DataReaderType,

                /// <summary>
                ///  Uses the return type specified in <see cref="AdoNetClientInstrumentMethodsAttribute.DataReaderTaskType" />
                /// </summary>
                DataReaderTaskType,
            }

            /// <summary>
            /// Gets or sets the name of the target method to be intercepted.
            /// If null, default to the name of the decorated method.
            /// </summary>
            public string MethodName { get; set; }

            /// <summary>
            /// Gets or sets the return type name
            /// </summary>
            public string ReturnTypeName { get; set; }

            /// <summary>
            /// Gets or sets the parameters type array for the target method to be intercepted.
            /// </summary>
            public string[] ParameterTypeNames { get; set; }

            /// <summary>
            /// Gets or sets the CallTarget Class used to instrument the method
            /// </summary>
            public Type CallTargetType { get; set; }

            /// <summary>
            /// Gets or sets the CallTarget integration type
            /// </summary>
            public CallTargetKind CallTargetIntegrationKind { get; set; } = CallTargetKind.Default;

            /// <summary>
            /// Gets or sets the return type to use with this signature
            /// </summary>
            public AdoNetTargetSignatureReturnType ReturnType { get; set; } = AdoNetTargetSignatureReturnType.Default;
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.Int32TaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteNonQueryAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteNonQueryAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            CallTargetType = typeof(CommandExecuteNonQueryIntegration),
            CallTargetIntegrationKind = CallTargetKind.Default)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteNonQueryAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            CallTargetType = typeof(CommandExecuteNonQueryIntegration),
            CallTargetIntegrationKind = CallTargetKind.Derived)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteNonQueryDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteNonQueryWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteNonQueryWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            CallTargetType = typeof(CommandExecuteReaderAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderWithCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderWithBehaviorAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReaderAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReader,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderType,
            CallTargetType = typeof(CommandExecuteReaderIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReader,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteReaderWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReader,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteDbDataReaderWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReader,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetIntegrationKind = CallTargetKind.Derived,
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteDbDataReaderWithBehaviorDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalarAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.ObjectTaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteScalarAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteScalarAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            CallTargetType = typeof(CommandExecuteScalarIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteScalarAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            CallTargetIntegrationKind = CallTargetKind.Derived,
            CallTargetType = typeof(CommandExecuteScalarIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteScalarDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteScalarWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal class CommandExecuteScalarWithBehaviorAttribute : Attribute
        {
        }
    }

    /// <summary>
    /// CallTarget instrumentation for:
    /// int [Command].ExecuteNonQuery()
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CommandExecuteNonQueryIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            return new CallTargetState(DbScopeFactory.Cache<TTarget>.CreateDbCommandScope(Tracer.Instance, (IDbCommand)instance));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    /// <summary>
    /// CallTarget instrumentation for:
    /// [*]DataReader [Command].ExecuteReader()
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CommandExecuteReaderIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            return new CallTargetState(DbScopeFactory.Cache<TTarget>.CreateDbCommandScope(Tracer.Instance, (IDbCommand)instance));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

}
""";
        }
    }
}
