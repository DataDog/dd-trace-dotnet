using System;
using System.Collections.Concurrent;
using System.Reflection;
using Datadog.Trace.Util;
using Sigil;

namespace Datadog.Trace.ClrProfiler.Emit
{
    /// <summary>
    /// Provides helper methods to access object members by emitting IL dynamically.
    /// </summary>
    internal static class ObjectExtensions
    {
        private static readonly ConcurrentDictionary<string, object> Cache = new ConcurrentDictionary<string, object>();
        private static readonly ConcurrentDictionary<string, PropertyFetcher> PropertyFetcherCache = new ConcurrentDictionary<string, PropertyFetcher>();

        /// <summary>
        /// Tries to call an instance method with the specified name, a single parameter, and a return value.
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
            var paramType1 = typeof(TArg1);

            object cachedItem = Cache.GetOrAdd(
                $"{type.AssemblyQualifiedName}.{methodName}.{paramType1.AssemblyQualifiedName}",
                key =>
                    DynamicMethodBuilder<Func<object, TArg1, TResult>>
                       .CreateMethodCallDelegate(
                            type,
                            methodName,
                            OpCodeValue.Callvirt,
                            methodParameterTypes: new[] { paramType1 }));

            if (cachedItem is Func<object, TArg1, TResult> func)
            {
                value = func(source, arg1);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Tries to call an instance method with the specified name and a return value.
        /// </summary>
        /// <typeparam name="TResult">The type of the method's result value.</typeparam>
        /// <param name="source">The object to call the method on.</param>
        /// <param name="methodName">The name of the method to call.</param>
        /// <param name="value">The value returned by the method.</param>
        /// <returns><c>true</c> if the method was found, <c>false</c> otherwise.</returns>
        public static bool TryCallMethod<TResult>(this object source, string methodName, out TResult value)
        {
            var type = source.GetType();

            object cachedItem = Cache.GetOrAdd(
                $"{type.AssemblyQualifiedName}.{methodName}",
                key =>
                    DynamicMethodBuilder<Func<object, TResult>>
                       .CreateMethodCallDelegate(
                            type,
                            methodName,
                            OpCodeValue.Callvirt));

            if (cachedItem is Func<object, TResult> func)
            {
                value = func(source);
                return true;
            }

            value = default;
            return false;
        }

        public static MemberResult<TResult> CallMethod<TArg1, TResult>(this object source, string methodName, TArg1 arg1)
        {
            return source.TryCallMethod(methodName, arg1, out TResult result)
                       ? new MemberResult<TResult>(result)
                       : MemberResult<TResult>.NotFound;
        }

        public static MemberResult<object> CallMethod<TArg1>(this object source, string methodName, TArg1 arg1)
        {
            return CallMethod<TArg1, object>(source, methodName, arg1);
        }

        public static MemberResult<TResult> CallMethod<TResult>(this object source, string methodName)
        {
            return source.TryCallMethod(methodName, out TResult result)
                       ? new MemberResult<TResult>(result)
                       : MemberResult<TResult>.NotFound;
        }

        /// <summary>
        /// Tries to get the value of an instance property with the specified name.
        /// </summary>
        /// <typeparam name="TResult">The type of the property.</typeparam>
        /// <param name="source">The value that contains the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value of the property, or <c>null</c> if the property is not found.</param>
        /// <returns><c>true</c> if the property exists, otherwise <c>false</c>.</returns>
        public static bool TryGetPropertyValue<TResult>(this object source, string propertyName, out TResult value)
        {
            if (source != null)
            {
                var type = source.GetType();

                PropertyFetcher fetcher = PropertyFetcherCache.GetOrAdd(
                    GetKey(propertyName, type),
                    key => new PropertyFetcher(propertyName));

                if (fetcher != null)
                {
                    value = (TResult)fetcher.Fetch(source);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static MemberResult<TResult> GetProperty<TResult>(this object source, string propertyName)
        {
            if (source == null)
            {
                return MemberResult<TResult>.NotFound;
            }

            return source.TryGetPropertyValue(propertyName, out TResult result)
                       ? new MemberResult<TResult>(result)
                       : MemberResult<TResult>.NotFound;
        }

        public static MemberResult<object> GetProperty(this object source, string propertyName)
        {
            return GetProperty<object>(source, propertyName);
        }

        /// <summary>
        /// Tries to get the value of an instance field with the specified name.
        /// </summary>
        /// <typeparam name="TResult">The type of the field.</typeparam>
        /// <param name="source">The value that contains the field.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="value">The value of the field, or <c>null</c> if the field is not found.</param>
        /// <returns><c>true</c> if the field exists, otherwise <c>false</c>.</returns>
        public static bool TryGetFieldValue<TResult>(this object source, string fieldName, out TResult value)
        {
            var type = source.GetType();

            object cachedItem = Cache.GetOrAdd(
                GetKey(fieldName, type),
                key => CreateFieldDelegate<TResult>(type, fieldName));

            if (cachedItem is Func<object, TResult> func)
            {
                value = func(source);
                return true;
            }

            value = default;
            return false;
        }

        public static MemberResult<TResult> GetField<TResult>(this object source, string fieldName)
        {
            return source.TryGetFieldValue(fieldName, out TResult result)
                       ? new MemberResult<TResult>(result)
                       : MemberResult<TResult>.NotFound;
        }

        public static MemberResult<object> GetField(this object source, string fieldName)
        {
            return GetField<object>(source, fieldName);
        }

        private static string GetKey(string name, Type type)
        {
            return $"{type.AssemblyQualifiedName}:{name}";
        }

        private static Func<object, TResult> CreatePropertyDelegate<TResult>(Type containerType, string propertyName)
        {
            PropertyInfo propertyInfo = containerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (propertyInfo == null)
            {
                return null;
            }

            var dynamicMethod = Emit<Func<object, TResult>>.NewDynamicMethod($"{containerType.FullName}.get_{propertyName}");
            dynamicMethod.LoadArgument(0);

            if (containerType.IsValueType)
            {
                dynamicMethod.Unbox(containerType);
            }
            else
            {
                dynamicMethod.CastClass(containerType);
            }

            MethodInfo methodInfo = propertyInfo.GetMethod;

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

            if (propertyInfo.PropertyType != typeof(TResult))
            {
                if (propertyInfo.PropertyType.IsValueType)
                {
                    dynamicMethod.Box(propertyInfo.PropertyType);
                }

                dynamicMethod.CastClass(typeof(TResult));
            }

            dynamicMethod.Return();
            return dynamicMethod.CreateDelegate();
        }

        private static Func<object, TResult> CreateFieldDelegate<TResult>(Type containerType, string fieldName)
        {
            FieldInfo fieldInfo = containerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null)
            {
                return null;
            }

            var dynamicMethod = Emit<Func<object, TResult>>.NewDynamicMethod($"{containerType.FullName}.{fieldName}");
            dynamicMethod.LoadArgument(0);

            if (containerType.IsValueType)
            {
                dynamicMethod.UnboxAny(containerType);
            }
            else
            {
                dynamicMethod.CastClass(containerType);
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
            return dynamicMethod.CreateDelegate();
        }
    }
}
