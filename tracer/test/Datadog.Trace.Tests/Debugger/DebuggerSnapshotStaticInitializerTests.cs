// <copyright file="DebuggerSnapshotStaticInitializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using FluentAssertions;
using Xunit;

// StaticField is only read reflectively, so the compiler flags it as unused.
#pragma warning disable CS0414

namespace Datadog.Trace.Tests.Debugger
{
    /// <summary>
    /// Regression tests for snapshot static-field capture. Reading a static field
    /// (field.GetValue(null)) forces the declaring type's static constructor to run, which could
    /// poison the type. Static-field capture was removed; these tests fail on the old behavior.
    /// </summary>
    public class DebuggerSnapshotStaticInitializerTests
    {
        [Fact]
        public void MethodProbeEntry_DoesNotRunDeclaringTypeStaticConstructor()
        {
            Tracker.MechanismCctorRan.Should().BeFalse();

            RunMethodProbeEntry(typeof(TypeWithObservableStaticInitializer));

            // Capture must not read the type's statics, so its .cctor is not forced to run.
            Tracker.MechanismCctorRan.Should().BeFalse();
        }

        [Fact]
        public void MethodProbeEntry_OnTypeWithPreconditionDependentCctor_DoesNotPoisonType()
        {
            Tracker.ApplicationIsReady = false;

            // Capturing before the app is ready must not trigger the fragile .cctor.
            Action capture = () => RunMethodProbeEntry(typeof(TypeWithPreconditionDependentStaticInitializer));
            capture.Should().NotThrow();

            // The type is not poisoned, so normal use succeeds once the app is ready.
            Tracker.ApplicationIsReady = true;
            TypeWithPreconditionDependentStaticInitializer.ReadConfig().Should().Be("loaded");
        }

        private static void RunMethodProbeEntry(Type invocationTargetType)
        {
            var snapshotCreator = new DebuggerSnapshotCreator(
                isFullSnapshot: true,
                Datadog.Trace.Debugger.Expressions.ProbeLocation.Method,
                hasCondition: false,
                tags: [],
                limitInfo: new CaptureLimitInfo(
                    MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
                    MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
                    MaxLength: DebuggerSettings.DefaultMaxStringLength,
                    MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy),
                processTagsProvider: static () => null,
                serviceNameProvider: static () => "test-service");

            var captureInfo = new CaptureInfo<Type>(
                methodMetadataIndex: 0,
                methodState: MethodState.EntryStart,
                type: invocationTargetType,
                invocationTargetType: invocationTargetType);

            // The entry point DI and Exception Replay call when capturing a snapshot.
            snapshotCreator.CaptureEntryMethodStartMarker(ref captureInfo);
        }

        // Separate type so reading a flag does not trigger the type under test's .cctor.
        private static class Tracker
        {
            public static bool MechanismCctorRan { get; set; }

            public static bool ApplicationIsReady { get; set; }
        }

        private class TypeWithObservableStaticInitializer
        {
            private static readonly string StaticField;

            static TypeWithObservableStaticInitializer()
            {
                Tracker.MechanismCctorRan = true;
                StaticField = "set-by-cctor";
            }
        }

        private class TypeWithPreconditionDependentStaticInitializer
        {
            private static readonly string Config;

            // Fragile .cctor: throws unless the app is ready, like a container-resolving initializer.
            static TypeWithPreconditionDependentStaticInitializer()
            {
                if (!Tracker.ApplicationIsReady)
                {
                    throw new InvalidOperationException("Static initializer ran before the application was ready.");
                }

                Config = "loaded";
            }

            public static string ReadConfig() => Config;
        }
    }
}
