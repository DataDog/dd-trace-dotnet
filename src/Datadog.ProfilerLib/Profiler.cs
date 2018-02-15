using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Datadog.ProfilerLib
{
    internal static class ExpressionExtensions
    {
        internal static MethodInfo GetMethodInfo(this LambdaExpression expression)
        {
            var method = expression.Body as MethodCallExpression;
            if(method == null)
            {
                throw new ArgumentException("Expressions provided to instrumentation should be method calls");
            }
            return method.Method;
        }
    }

    public class Profiler
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Instrument(string appDomain, string assemblyName, string moduleName, string methodName, IntPtr before, IntPtr after, int numberOfArgs)
        {
            return;
        }

        private static void CheckInstrumentedMethod(MethodInfo method)
        {
            if (method.IsGenericMethod || method.IsGenericMethodDefinition)
            {
                throw new ArgumentException("Instrumentation error: the instrumented method cannot be generic");
            }
        }

        private static void CheckBeforeMethod(MethodInfo instrumentedMethod, MethodInfo before)
        {
            if(!before.IsStatic)
            {
                throw new ArgumentException("Instrumentation error: before method should be static");
            }
            if(before.ReturnType != typeof(Object))
            {
                throw new ArgumentException("Instrumentation error: before method should return an object");
            }
            var beforeParams = before.GetParameters();
            var expectedNumberOfParams = instrumentedMethod.GetParameters().Length;
            if (!instrumentedMethod.IsStatic)
            {
                expectedNumberOfParams++;
            }
            if(beforeParams.Length != expectedNumberOfParams)
            {
                throw new ArgumentException("Instrumentation error: before method does not have the right number of arguments");
            }
            var idx = 0;
            if(!instrumentedMethod.IsStatic)
            {
                if(beforeParams[idx].ParameterType != typeof(object))
                {
                    throw new ArgumentException("Instrumentation error: when instrumenting a non static method the first parameter of the before method should have type Object");
                }
                idx++;
            }
            foreach(var param in instrumentedMethod.GetParameters())
            {
                if(beforeParams[idx].ParameterType != param.ParameterType)
                {
                    throw new ArgumentException("Instrumentation error: before method should take the same arguments as the instrumented method)");
                }
                idx++;
            }
        }

        private static void CheckAfterMethod(MethodInfo instrumentedMethod, MethodInfo after)
        {
            if(!after.IsStatic)
            {
                throw new ArgumentException("Instrumentation error: after method should be static");
            }
            if(after.ReturnType != instrumentedMethod.ReturnType)
            {
                throw new ArgumentException("Instrumentation error: after method should have the same return type as instrumented method");
            }
            var afterParams = after.GetParameters();
            var expectedNumberOfParams = instrumentedMethod.GetParameters().Length + 1;
            if(instrumentedMethod.ReturnType != typeof(void))
            {
                expectedNumberOfParams++;
            }
            if (!instrumentedMethod.IsStatic)
            {
                expectedNumberOfParams++;
            }
            if (afterParams.Length != expectedNumberOfParams)
            {
                throw new ArgumentException("Instrumentation error: after method does not have the right number of arguments");
            }
            var idx = 0;
            if(!instrumentedMethod.IsStatic)
            {
                if(afterParams[idx].ParameterType != typeof(object))
                {
                    throw new ArgumentException("Instrumentation error: when instrumenting a non static method the first parameter of the after method should have type Object");
                }
                idx++;
            }
            foreach(var param in instrumentedMethod.GetParameters())
            {
                if(afterParams[idx].ParameterType != param.ParameterType)
                {
                    throw new ArgumentException("Instrumentation error: after method should have the same first arguments as the instrumented method)");
                }
                idx++;
            }
            if(afterParams[idx].ParameterType != typeof(Object))
            {
                throw new ArgumentException("Instrumentation error: after method context should be of type Object");
            }
            idx++;
            if (instrumentedMethod.ReturnType != typeof(void) && afterParams[idx].ParameterType != instrumentedMethod.ReturnType)
            {
                throw new ArgumentException("Instrumentation error: after method return parameter should be of same type as the instrumented method");
            }
            idx++;
        }

        private static void CheckExceptionMethod(MethodInfo instrumentedMethod, MethodInfo exception)
        {
            if(!exception.IsStatic)
            {
                throw new ArgumentException("Instrumentation error: exception method should be static");
            }
            if(exception.ReturnType != typeof(void))
            {
                throw new ArgumentException("Instrumentation error: exception method should have void return type");
            }
            var afterParams = exception.GetParameters();
            var expectedNumberOfParams = instrumentedMethod.GetParameters().Length + 2;
            if (!instrumentedMethod.IsStatic)
            {
                expectedNumberOfParams++;
            }
            if (afterParams.Length != expectedNumberOfParams)
            {
                throw new ArgumentException("Instrumentation error: exception method does not have the right number of arguments");
            }
            var idx = 0;
            if(!instrumentedMethod.IsStatic)
            {
                if(afterParams[idx].ParameterType != typeof(object))
                {
                    throw new ArgumentException("Instrumentation error: when instrumenting a non static method the first parameter of the exception method should have type Object");
                }
                idx++;
            }
            foreach(var param in instrumentedMethod.GetParameters())
            {
                if(afterParams[idx].ParameterType != param.ParameterType)
                {
                    throw new ArgumentException("Instrumentation error: after method should have the same first arguments as the instrumented method)");
                }
                idx++;
            }
            if(afterParams[idx].ParameterType != typeof(Object))
            {
                throw new ArgumentException("Instrumentation error: exception method context should be of type Object");
            }
            idx += 1;
            if(afterParams[idx].ParameterType != typeof(Exception))
            {
                throw new ArgumentException("Instrumentation error: last parameter of exception should be of type Exception");
            }
        }
        
        // Helpers to instrument static methods
        public static void Instrument(Expression<Action> methodToInstrument, Expression<Func<Object>> before, Expression<Action> after, Expression<Action> exception=null)
        {
            Instrument(methodToInstrument.GetMethodInfo(), before.GetMethodInfo(), after.GetMethodInfo(), exception?.GetMethodInfo());
        }

        public static void Instrument<T>(Expression<Func<T>> methodToInstrument, Expression<Func<Object>> before, Expression<Func<T>> after, Expression<Action> exception=null)
        {
            Instrument(methodToInstrument.GetMethodInfo(), before.GetMethodInfo(), after.GetMethodInfo(), exception?.GetMethodInfo());
        }

        // Helpers to instrument non static methods
        public static void Instrument<T>(Expression<Action<T>> methodToInstrument, Expression<Func<Object>> before, Expression<Action> after, Expression<Action> exception=null)
        {
            Instrument(methodToInstrument.GetMethodInfo(), before.GetMethodInfo(), after.GetMethodInfo(), exception?.GetMethodInfo());
        }

        public static void Instrument<T, U>(Expression<Func<T,U>> methodToInstrument, Expression<Func<Object>> before, Expression<Func<U>> after, Expression<Action> exception=null)
        {
            Instrument(methodToInstrument.GetMethodInfo(), before.GetMethodInfo(), after.GetMethodInfo(), exception?.GetMethodInfo());
        }

        public static void Instrument(MethodInfo m, MethodInfo before, MethodInfo after, MethodInfo exception = null)
        {
            if (!(m.IsGenericMethod || m.IsGenericMethodDefinition))
            {
                CheckBeforeMethod(m, before);
                CheckAfterMethod(m, after);
            }
            //CheckInstrumentedMethod(m);
            RuntimeHelpers.PrepareMethod(before.MethodHandle);
            RuntimeHelpers.PrepareMethod(after.MethodHandle);
            var beforePtr = before.MethodHandle.GetFunctionPointer();
            var afterPtr = after.MethodHandle.GetFunctionPointer();
            IntPtr exceptionPtr = IntPtr.Zero;
            if (exception != null)
            {
                if (!(m.IsGenericMethod || m.IsGenericMethodDefinition))
                {
                    CheckExceptionMethod(m, exception);
                }
                RuntimeHelpers.PrepareMethod(exception.MethodHandle);
                exceptionPtr = exception.MethodHandle.GetFunctionPointer();
            }
            var methodToken = m.MetadataToken;
            var moduleName = m.Module.Name;
            var assemblyName = m.Module.Assembly.GetName().Name;
            var appDomain = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine($"Requesting instrumentation for: {appDomain}, {methodToken:x}, {moduleName}, {assemblyName}");
            var isSuccess = InstrumentInternal(assemblyName, moduleName, methodToken, beforePtr, afterPtr, exceptionPtr);
            if(!isSuccess)
            {
                throw new Exception("Unexpected error while instrumenting the method");
            }
        }

        // TODO make me private
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool InstrumentInternal(string assemblyName, string moduleName, int methodToken, IntPtr before, IntPtr after, IntPtr exception)
        {
            return false;
        }
    }
}
