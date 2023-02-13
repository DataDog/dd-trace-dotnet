using System;
using System.Globalization;

namespace Samples.Probes.TestRuns.Shared
{
    public interface IGenericInterface<T>
    {
        public T Value { get; set; }
    }

    public interface IInterface
    {
#if NETCOREAPP3_0_OR_GREATER
        // we should not see this property in the snapshot
        public string CallToString => DateTime.Now.ToString(CultureInfo.CurrentCulture);
#endif
        public string DoNotShowMe { get; set; }
        public string ShowMe { get; set; }
    }

    internal class Class : IInterface
    {
        internal string Field;

        public Class()
        {
            Field = "I'm a class field";
        }

        private string _privateField;
        public string DoNotShowMe
        {
            get
            {
                _privateField = "This string should never be visible";
                return $"Do not show me!: {_privateField}";
            }
            set
            {
                _privateField = value;
            }
        }

        public string ShowMe { get; set; }
    }

    internal class Class<T> : IGenericInterface<T> where T : class
    {
        internal T GenericValue;

        public Class()
        {
            GenericValue = "I'm a generic field" as T;
        }

        public T Value { get; set; }
    }
}
