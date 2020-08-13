using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Util
{
    internal class FieldFetcher : IMemberFetcher
    {
        private readonly string _fieldName;
        private Type _expectedType;
        private object _fetchForExpectedType;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldFetcher"/> class.
        /// </summary>
        /// <param name="fieldName">The name of the field that this instance will fetch.</param>
        public FieldFetcher(string fieldName)
        {
            _fieldName = fieldName;
        }

        /// <summary>
        /// Gets the value of the field on the specified object.
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="obj">The object that contains the field.</param>
        /// <returns>The value of the field on the specified object.</returns>
        public T Fetch<T>(object obj)
        {
            return Fetch<T>(obj, obj.GetType());
        }

        /// <summary>
        /// Gets the value of the field on the specified object.
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="obj">The object that contains the field.</param>
        /// <param name="objType">Type of the object</param>
        /// <returns>The value of the field on the specified object.</returns>
        public T Fetch<T>(object obj, Type objType)
        {
            if (objType != _expectedType)
            {
                var fieldInfo = objType.GetField(_fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                _fetchForExpectedType = FieldFetch<T>.FetcherForField(fieldInfo);
                _expectedType = objType;
            }

            return ((FieldFetch<T>)_fetchForExpectedType).Fetch(obj);
        }

        /// <summary>
        /// FieldFetch is a helper class. It takes a FieldInfo and then knows how
        /// to efficiently fetch that field from a .NET object (See Fetch method).
        /// It hides some slightly complex generic code.
        /// </summary>
        /// <typeparam name="T">Return type of the field.</typeparam>
        private class FieldFetch<T>
        {
            private readonly Func<object, T> _fieldFetch;

            private FieldFetch()
            {
                _fieldFetch = _ => default;
            }

            private FieldFetch(FieldInfo fieldInfo)
            {
                // Generate lambda: arg => (T)((TObject)arg).field;
                var param = Expression.Parameter(typeof(object), "arg"); // arg =>
                var cast = Expression.Convert(param, fieldInfo.DeclaringType); // (TObject)arg
                var fieldFetch = Expression.Field(cast, fieldInfo); // field
                var castResult = Expression.Convert(fieldFetch, typeof(T)); // (T)result

                // Generate the actual lambda
                var lambda = Expression.Lambda(typeof(Func<object, T>), castResult, param);

                // Compile it for faster access
                _fieldFetch = (Func<object, T>)lambda.Compile();
            }

            /// <summary>
            /// Create a field fetcher from a .NET Reflection <see cref="FieldInfo"/> class that
            /// represents a field of a particular type.
            /// </summary>
            /// <param name="fieldInfo">The field that this instance will fetch.</param>
            /// <returns>The new field fetcher.</returns>
            public static FieldFetch<T> FetcherForField(FieldInfo fieldInfo)
            {
                if (fieldInfo == null)
                {
                    // returns null on any fetch.
                    return new FieldFetch<T>();
                }

                return new FieldFetch<T>(fieldInfo);
            }

            /// <summary>
            /// Gets the value of the field on the specified object.
            /// </summary>
            /// <param name="obj">The object that contains the field.</param>
            /// <returns>The value of the field on the specified object.</returns>
            public T Fetch(object obj)
            {
                return _fieldFetch(obj);
            }
        }
    }
}
