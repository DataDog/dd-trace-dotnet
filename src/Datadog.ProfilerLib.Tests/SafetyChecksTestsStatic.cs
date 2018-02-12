using System;
using Xunit;

namespace Datadog.ProfilerLib.Tests
{
    public class SafetyChecksTestsStatic
    {
        public static void MethodToInstrumentIntArg(int a)
        {
        }

        public static Object BeforeIntArg(int a)
        {
            return null;
        }

        public static void AfterIntArg(int a, Object context)
        {
        }

        public static void ExceptionIntArg(int a, Object context, Exception ex)
        {
        }

        [Fact]
        public void InstrumentIntArg_RightTypes_ShouldSucceed()
        {
            Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                );
        }

        public static Object BeforeNoArg()
        {
            return null;
        }

        [Fact]
        public void InstrumentIntArg_WithMissingBeforeParam_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeNoArg(),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }
        public static Object BeforeString(string s)
        {
            return null;
        }

        [Fact]
        public void InstrumentIntArg_WithWrongBeforeParam_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeString(default(string)),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static void AfterNoArg()
        {
        }

        [Fact]
        public void InstrumentIntArg_WithMissingAfterParam_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterNoArg(),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static void AfterIntArgNoContext(int a)
        {
        }

        [Fact]
        public void InstrumentIntArg_WithMissingAfterContext_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArgNoContext(default(int)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static void AfterStringArg(string a, Object context)
        {
        }

        [Fact]
        public void InstrumentIntArg_WithAfterWrongType_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterStringArg(default(string), default(object)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static void AfterIntArgIntContext(int a, int context)
        {
        }

        [Fact]
        public void InstrumentIntArg_WithAfterWrongContextType_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArgIntContext(default(int), default(int)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static Object AfterIntArgReturnObj(int a, Object context)
        {
            return null;
        }

        [Fact]
        public void InstrumentIntArg_WithAfterNonVoidReturn_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArgReturnObj(default(int), default(object)),
                () => ExceptionIntArg(default(int), default(object), default(Exception))
                ));
        }

        public static void ExceptionNoArg()
        {
        }

        [Fact]
        public void InstrumentIntArg_WithMissingExceptionParam_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionNoArg()
                ));
        }

        public static void ExceptionIntArgIntContext(int a, int context, Exception ex)
        {
        }

        [Fact]
        public void InstrumentIntArg_WithWrongContextType_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionIntArgIntContext(default(int), default(int), default(Exception))
                ));
        }

        public static void ExceptionIntArgObjectException(int a, Object context, Object ex)
        {
        }

        [Fact]
        public void InstrumentIntArg_WithWrongExceptionType_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => Profiler.Instrument(
                () => MethodToInstrumentIntArg(default(int)),
                () => BeforeIntArg(default(int)),
                () => AfterIntArg(default(int), default(object)),
                () => ExceptionIntArgObjectException(default(int), default(Object), default(Object))
                ));
        }
    }
}
