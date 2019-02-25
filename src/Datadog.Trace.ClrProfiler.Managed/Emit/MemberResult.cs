using System;

namespace Datadog.Trace.ClrProfiler.Emit
{
    internal sealed class MemberResult<T>
    {
        /// <summary>
        /// A static value used to represent a member that was not found.
        /// </summary>
        public static readonly MemberResult<T> NotFound = new MemberResult<T>();

        private readonly T _value;

        public MemberResult(T value)
        {
            _value = value;
        }

        private MemberResult()
        {
        }

        public T Value =>
            ReferenceEquals(this, NotFound)
                ? throw new InvalidOperationException("Reflected member not found.")
                : _value;

        public T GetValueOrDefault()
        {
            return _value;
        }

        public MemberResult<TResult> GetProperty<TResult>(string propertyName)
        {
            if (ReferenceEquals(this, NotFound) || Value == null || !Value.TryGetPropertyValue(propertyName, out TResult result))
            {
                return MemberResult<TResult>.NotFound;
            }

            return new MemberResult<TResult>(result);
        }

        public MemberResult<object> GetProperty(string propertyName)
        {
            return GetProperty<object>(propertyName);
        }

        public MemberResult<TResult> GetField<TResult>(string fieldName)
        {
            if (ReferenceEquals(this, NotFound) || Value == null || !Value.TryGetFieldValue(fieldName, out TResult result))
            {
                return MemberResult<TResult>.NotFound;
            }

            return new MemberResult<TResult>(result);
        }

        public MemberResult<object> GetField(string fieldName)
        {
            return GetField<object>(fieldName);
        }

        public MemberResult<TResult> CallMethod<TArg1, TResult>(string methodName, TArg1 arg1)
        {
            if (ReferenceEquals(this, NotFound) || Value == null || !Value.TryCallMethod(methodName, arg1, out TResult result))
            {
                return MemberResult<TResult>.NotFound;
            }

            return new MemberResult<TResult>(result);
        }

        public MemberResult<object> CallMethod<TArg1>(string methodName, TArg1 arg1)
        {
            return CallMethod<TArg1, object>(methodName, arg1);
        }

        public override string ToString()
        {
            if (ReferenceEquals(this, NotFound) || Value == null)
            {
                return string.Empty;
            }

            return Value.ToString();
        }
    }
}
