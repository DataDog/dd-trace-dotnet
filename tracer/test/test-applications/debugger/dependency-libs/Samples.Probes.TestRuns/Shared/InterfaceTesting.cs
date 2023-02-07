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
#if NETCOREAPP3_1_OR_GREATER
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

        private string _notAutoProp;
        public string DoNotShowMe
        {
            get
            {
                _notAutoProp = ToString();
                return $"Do not show me!: {_notAutoProp}";
            }
            set
            {
                _notAutoProp = value;
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
