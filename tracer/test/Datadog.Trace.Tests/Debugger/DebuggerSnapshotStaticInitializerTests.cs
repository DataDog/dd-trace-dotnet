// <copyright file="DebuggerSnapshotStaticInitializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

// The demonstration types below intentionally have static fields that are only ever read
// reflectively by the snapshot serializer, so the compiler flags them as "assigned but never used".
#pragma warning disable CS0414

namespace Datadog.Trace.Tests.Debugger
{
    /// <summary>
    /// Demonstrates that Dynamic Instrumentation's snapshot capture forces a type's
    /// static constructor (.cctor / type initializer) to run at probe-fire time.
    ///
    /// When a snapshot is captured, <see cref="DebuggerSnapshotCreator.CaptureStaticFields{T}"/>
    /// calls <see cref="DebuggerSnapshotSerializer.SerializeStaticFields"/> for the instrumented
    /// method's declaring type. That reads every static field via <c>FieldInfo.GetValue(null)</c>,
    /// and a static-field read is a trigger point for the declaring type's type initializer.
    ///
    /// For a type the application has not yet touched, this runs its .cctor earlier than the
    /// application's own code would have. If that .cctor has an ordering/precondition dependency
    /// and throws, the CLR permanently caches the failure and rethrows it on every later access,
    /// crashing otherwise-correct application code.
    ///
    /// The demonstration types use an explicit static constructor (so they are NOT 'beforefieldinit')
    /// purely to make the tests deterministic: with precise initialization semantics the .cctor
    /// provably has not run until a static field is first accessed. In the wild, 'beforefieldinit'
    /// types (the C# default for types with only static field initializers) are even more exposed,
    /// because the application can hold live instances of them while their .cctor has still not run.
    /// </summary>
    public class DebuggerSnapshotStaticInitializerTests
    {
        [Fact]
        public void SerializeStaticFields_ForcesStaticConstructorToRunEagerly()
        {
            // The type has not been touched, so its static constructor has not run.
            Tracker.MechanismCctorRan.Should().BeFalse();

            // DI captures the static fields of an instrumented method's declaring type.
            SerializeStaticFields(typeof(TypeWithObservableStaticInitializer));

            // Capturing the snapshot ran the static constructor, because the serializer
            // reads each static field via field.GetValue(null), which triggers the type initializer.
            Tracker.MechanismCctorRan.Should().BeTrue();
        }

        [Fact]
        public void SerializeStaticFields_RunningCctorTooEarly_PoisonsType_AndBreaksLaterApplicationUse()
        {
            // The application has not finished initializing yet (e.g. configuration not loaded).
            Tracker.ApplicationIsReady = false;

            // A probe fires early and DI captures a snapshot. The serializer wraps every static read
            // in a try/catch, so the capture itself "succeeds" and silently hides the problem.
            Action capture = () => SerializeStaticFields(typeof(TypeWithPreconditionDependentStaticInitializer));
            capture.Should().NotThrow("the serializer swallows exceptions thrown while reading a static member");

            // The application has now finished initializing and legitimately uses the type.
            Tracker.ApplicationIsReady = true;

            // But the type is permanently poisoned: the CLR cached the type-initialization failure
            // that DI triggered too early, and rethrows it on every later access. Correct application
            // code now crashes through no fault of its own.
            Action applicationUse = () => TypeWithPreconditionDependentStaticInitializer.ReadConfig();
            applicationUse.Should().Throw<TypeInitializationException>();
        }

        [Fact]
        public void CaptureEntryMethodStartMarker_RunsDeclaringTypeStaticConstructor()
        {
            // This drives the actual entry point our method-probe instrumentation calls at method entry,
            // proving the eager static read is reached through the real capture flow (not just the
            // low-level serializer helper).
            Tracker.EntryPointCctorRan.Should().BeFalse();

            var snapshotCreator = new DebuggerSnapshotCreator(
                isFullSnapshot: true,
                Datadog.Trace.Debugger.Expressions.ProbeLocation.Method,
                hasCondition: false,
                tags: [],
                limitInfo: CreateLimitInfo(),
                processTagsProvider: static () => null,
                serviceNameProvider: static () => "test-service");

            var method = typeof(TypeProbedAtEntry).GetMethod(nameof(TypeProbedAtEntry.InstanceMethodWithProbe));
            var captureInfo = new CaptureInfo<Type>(
                methodMetadataIndex: 0,
                methodState: MethodState.EntryStart,
                method: method,
                type: typeof(TypeProbedAtEntry),
                invocationTargetType: typeof(TypeProbedAtEntry));

            snapshotCreator.CaptureEntryMethodStartMarker(ref captureInfo);

            Tracker.EntryPointCctorRan.Should().BeTrue(
                "CaptureEntryMethodStartMarker -> CaptureStaticFields -> SerializeStaticFields read the type's static fields");
        }

        private static void SerializeStaticFields(Type declaringType)
        {
            using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.WriteStartObject();
            DebuggerSnapshotSerializer.SerializeStaticFields(declaringType, jsonWriter, CreateLimitInfo());
            jsonWriter.WriteEndObject();
        }

        private static CaptureLimitInfo CreateLimitInfo()
        {
            return new CaptureLimitInfo(
                MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
                MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
                MaxLength: DebuggerSettings.DefaultMaxStringLength,
                MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy);
        }

        // Observation state lives in a separate type so that checking a flag does not itself
        // trigger the static constructor of the type under test.
        private static class Tracker
        {
            public static bool MechanismCctorRan { get; set; }

            public static bool EntryPointCctorRan { get; set; }

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

            static TypeWithPreconditionDependentStaticInitializer()
            {
                // A realistic .cctor that assumes the application reached a certain point first
                // (configuration loaded, DI container built, a singleton constructed, etc.).
                if (!Tracker.ApplicationIsReady)
                {
                    throw new InvalidOperationException("Static initializer ran before the application was ready.");
                }

                Config = "loaded";
            }

            // Stands in for ordinary application code that reads the type's static state.
            public static string ReadConfig() => Config;
        }

        private class TypeProbedAtEntry
        {
            private static readonly string StaticField;

            static TypeProbedAtEntry()
            {
                Tracker.EntryPointCctorRan = true;
                StaticField = "set-by-cctor";
            }

            public void InstanceMethodWithProbe()
            {
            }
        }
    }
}
