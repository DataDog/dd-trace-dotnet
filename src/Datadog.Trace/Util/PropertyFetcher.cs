// From https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs

using System;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.Util
{
    internal class PropertyFetcher
    {
        private readonly string _propertyName;
        private Type _expectedType;
        private PropertyFetch _fetchForExpectedType;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyFetcher"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property that this instance will fetch.</param>
        public PropertyFetcher(string propertyName)
        {
            _propertyName = propertyName;
        }

        /// <summary>
        /// Gets the value of the property on the specified object.
        /// </summary>
        /// <param name="obj">The object that contains the property.</param>
        /// <returns>The value of the property on the specified object.</returns>
        public object Fetch(object obj)
        {
            Type objType = obj.GetType();

            if (objType != _expectedType)
            {
                var propertyInfo = objType.GetProperty(_propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _fetchForExpectedType = PropertyFetch.FetcherForProperty(propertyInfo);
                _expectedType = objType;
            }

            return _fetchForExpectedType.Fetch(obj);
        }

        /// <summary>
        /// PropertyFetch is a helper class. It takes a PropertyInfo and then knows how
        /// to efficiently fetch that property from a .NET object (See Fetch method).
        /// It hides some slightly complex generic code.
        /// </summary>
        private class PropertyFetch
        {
            /// <summary>
            /// Create a property fetcher from a .NET Reflection <see cref="PropertyInfo"/> class that
            /// represents a property of a particular type.
            /// </summary>
            /// <param name="propertyInfo">The property that this instance will fetch.</param>
            /// <returns>The new property fetcher.</returns>
            public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
            {
                if (propertyInfo == null)
                {
                    // returns null on any fetch.
                    return new PropertyFetch();
                }

                Type typedPropertyFetcher = typeof(TypedFetchProperty<,>);
                Type instantiatedTypedPropertyFetcher = typedPropertyFetcher.GetTypeInfo().MakeGenericType(
                    propertyInfo.DeclaringType, propertyInfo.PropertyType);

                return (PropertyFetch)Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
            }

            /// <summary>
            /// Gets the value of the property on the specified object.
            /// </summary>
            /// <param name="obj">The object that contains the property.</param>
            /// <returns>The value of the property on the specified object.</returns>
            public virtual object Fetch(object obj)
            {
                return null;
            }

            private class TypedFetchProperty<TObject, TProperty> : PropertyFetch
            {
                private readonly Func<TObject, TProperty> _propertyFetch;

                public TypedFetchProperty(PropertyInfo property)
                {
                    _propertyFetch = (Func<TObject, TProperty>)property.GetMethod.CreateDelegate(typeof(Func<TObject, TProperty>));
                }

                public override object Fetch(object obj)
                {
                    return _propertyFetch((TObject)obj);
                }
            }
        }
    }
}
