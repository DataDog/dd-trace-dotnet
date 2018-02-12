using System;
using Xunit;

namespace Datadog.ProfilerLib.Tests
{
    public class SafetyChecksTestsNonStatic
    {
        public void MethodToInstrumentIntArg(int a)
        {
        }

        public static Object BeforeIntArg(object thisObj, int a)
        {
            return null;
        }

        public static void AfterIntArg(object thisObj, int a, Object context)
        {
        }

        public static void ExceptionIntArg(object thisObj, int a, Object context, Exception ex)
        {
        }

        [Fact]
        public void InstrumentIntArg_RightTypes_ShouldSucceed()
        {
            Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(object), default(int)),
                () => AfterIntArg(default(object), default(int), default(object)),
                () => ExceptionIntArg(default(object), default(int), default(object), default(Exception))
                );
        }

        public static Object BeforeIntArgWithoutThis(int a)
        {
            return null;
        }

        [Fact]
        public void InstrumentIntArg_BeforeMissingThisObject_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArgWithoutThis(default(int)),
                () => AfterIntArg(default(object), default(int), default(object)),
                () => ExceptionIntArg(default(object), default(int), default(object), default(Exception))
                ));
        }

        public static void AfterIntArgWithoutThis(int a, Object context)
        {
        }

        [Fact]
        public void InstrumentIntArg_AfterMissingThisObject_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(object), default(int)),
                () => AfterIntArgWithoutThis(default(int), default(object)),
                () => ExceptionIntArg(default(object), default(int), default(object), default(Exception))
                ));
        }
    }
}
