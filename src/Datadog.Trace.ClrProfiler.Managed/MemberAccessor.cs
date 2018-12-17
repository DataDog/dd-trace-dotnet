using System;
using System.Reflection;
using Sigil;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides helper methods to access object members by emitting IL dynamically.
    /// </summary>
    public static class MemberAccessor
    {
        /// <summary>
        /// Tries to call a method with the specified name, a single parameter, and a return value.
        /// </summary>
        /// <typeparam name="TArg1">The type of the method's single parameter.</typeparam>
        /// <typeparam name="TResult">The type of the method's result value.</typeparam>
        /// <param name="source">The object to call the method on.</param>
        /// <param name="methodName">The name of the method to call.</param>
        /// <param name="arg1">The value to pass as the method's single argument.</param>
        /// <param name="value">The value returned by the method.</param>
        /// <returns><c>true</c> if the method was found, <c>false</c> otherwise.</returns>
        public static bool TryCallMethod<TArg1, TResult>(this object source, string methodName, TArg1 arg1, out TResult value)
        {
            var type = source.GetType();

            var func = DynamicMethodBuilder<Func<object, TArg1, TResult>>
               .CreateMethodCallDelegate(
                    type,
                    methodName,
                    methodParameterTypes: new[] { typeof(TArg1) });

            if (func == null)
            {
                value = default;
                return false;
            }

            value = func(source, arg1);
            return true;
        }

        /// <summary>
        /// Gets the value of the property in <paramref name="source"/>
        /// specified by <paramref name="propertyName"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the property.</typeparam>
        /// <param name="source">The value that contains the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value of the property, or <c>null</c> if the property is not found.</param>
        /// <returns><c>true</c> if the property exists, otherwise <c>false</c>.</returns>
        public static bool TryGetPropertyValue<TResult>(this object source, string propertyName, out TResult value)
        {
            var type = source.GetType();
            PropertyInfo propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (propertyInfo == null)
            {
                value = default;
                return false;
            }

            MethodInfo methodInfo = propertyInfo.GetMethod;

            var dynamicMethod = Emit<Func<object, TResult>>.NewDynamicMethod($"{type.FullName}.get_{propertyName}");
            dynamicMethod.LoadArgument(0);

            if (type.IsValueType)
            {
                dynamicMethod.Unbox(type);
            }
            else
            {
                dynamicMethod.CastClass(type);
            }

            if (methodInfo.IsStatic)
            {
                dynamicMethod.Call(methodInfo);
            }
            else
            {
                // C# compiler always uses CALLVIRT for instance methods
                // to get the cheap null check, even if they are not virtual
                dynamicMethod.CallVirtual(methodInfo);
            }

            if (propertyInfo.PropertyType.IsValueType && typeof(TResult) == typeof(object))
            {
                dynamicMethod.Box(propertyInfo.PropertyType);
            }
            else if (propertyInfo.PropertyType != typeof(TResult))
            {
                dynamicMethod.CastClass(typeof(TResult));
            }

            dynamicMethod.Return();

            // TODO: cache the dynamic method
            Func<object, TResult> func = dynamicMethod.CreateDelegate();
            value = func(source);
            return true;
        }

        /// <summary>
        /// Gets the value of the field in <paramref name="source"/>
        /// specified by <paramref name="fieldName"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the field.</typeparam>
        /// <param name="source">The value that contains the field.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="value">The value of the field, or <c>null</c> if the field is not found.</param>
        /// <returns><c>true</c> if the field exists, otherwise <c>false</c>.</returns>
        public static bool TryGetFieldValue<TResult>(this object source, string fieldName, out TResult value)
        {
            var type = source.GetType();
            FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null)
            {
                value = default;
                return false;
            }

            var dynamicMethod = Emit<Func<object, TResult>>.NewDynamicMethod($"{type.FullName}.{fieldName}");
            dynamicMethod.LoadArgument(0);

            if (type.IsValueType)
            {
                dynamicMethod.UnboxAny(type);
            }
            else
            {
                dynamicMethod.CastClass(type);
            }

            dynamicMethod.LoadField(fieldInfo);

            if (fieldInfo.FieldType.IsValueType && typeof(TResult) == typeof(object))
            {
                dynamicMethod.Box(fieldInfo.FieldType);
            }
            else if (fieldInfo.FieldType != typeof(TResult))
            {
                dynamicMethod.CastClass(typeof(TResult));
            }

            dynamicMethod.Return();

            // TODO: cache the dynamic method
            Func<object, TResult> func = dynamicMethod.CreateDelegate();
            value = func(source);
            return true;
        }
    }
}
