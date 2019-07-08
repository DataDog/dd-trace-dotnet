using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    /// <summary>
    /// Gives access to <see cref="MethodInfo"/> from expressions. This is useful
    /// because it allows static checking of the existence of the method.
    /// </summary>
    internal static class MethodReference
    {
        public static MethodInfo Get<T1>(Expression<Action<T1>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, TResult>(Expression<Func<T1, TResult>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, T2>(Expression<Action<T1, T2>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, T2, TResult>(Expression<Func<T1, T2, TResult>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, T2, T3>(Expression<Action<T1, T2, T3>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, T2, T3, TResult>(Expression<Func<T1, T2, T3, TResult>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<T1, T2, T3, T4>(Expression<Action<T1, T2, T3, T4>> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get(Expression<Action> method)
        {
            return GetMethodFromExpression(method);
        }

        public static MethodInfo Get<TResult>(Expression<Func<TResult>> method)
        {
            return GetMethodFromExpression(method);
        }

        private static MethodInfo GetMethodFromExpression(LambdaExpression method)
        {
            var methodCall = method.Body as MethodCallExpression;
            if (methodCall == null)
            {
                throw new ArgumentException("The expression must be a method call.", nameof(method));
            }

            return methodCall.Method;
        }
    }
}
