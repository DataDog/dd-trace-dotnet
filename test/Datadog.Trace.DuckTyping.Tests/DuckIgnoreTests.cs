using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckIgnoreTests
    {
        [Fact]
        public void NonPublicStructCopyTest()
        {
            PrivateStruct instance = default;
            CopyStruct copy = instance.DuckCast<CopyStruct>();
            Assert.Equal((int)instance.Value, (int)copy.Value);
            Assert.Equal(ValuesDuckType.Third.ToString(), copy.GetValue());
            Assert.Equal(ValuesDuckType.Third.ToString(), ((IGetValue)copy).GetValueProp);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void NonPublicStructInterfaceProxyTest()
        {
            PrivateStruct instance = default;
            IPrivateStruct proxy = instance.DuckCast<IPrivateStruct>();
            Assert.Equal((int)instance.Value, (int)proxy.Value);
            Assert.Equal(ValuesDuckType.Third.ToString(), proxy.GetValue());
            Assert.Equal(ValuesDuckType.Third.ToString(), proxy.GetValueProp);
        }
#endif

        [Fact]
        public void NonPublicStructAbstractProxyTest()
        {
            PrivateStruct instance = default;
            AbstractPrivateProxy proxy = instance.DuckCast<AbstractPrivateProxy>();
            Assert.Equal((int)instance.Value, (int)proxy.Value);
            Assert.Equal(ValuesDuckType.Third.ToString(), proxy.GetValue());
            Assert.Equal(ValuesDuckType.Third.ToString(), ((IGetValue)proxy).GetValueProp);
        }

        [Fact]
        public void NonPublicStructVirtualProxyTest()
        {
            PrivateStruct instance = default;
            VirtualPrivateProxy proxy = instance.DuckCast<VirtualPrivateProxy>();
            Assert.Equal((int)instance.Value, (int)proxy.Value);
            Assert.Equal(ValuesDuckType.Third.ToString(), proxy.GetValue());
            Assert.Equal(ValuesDuckType.Third.ToString(), ((IGetValue)proxy).GetValueProp);
        }

        [DuckCopy]
        public struct CopyStruct : IGetValue
        {
            public ValuesDuckType Value;

            string IGetValue.GetValueProp => Value.ToString();

            public string GetValue() => Value.ToString();
        }

#if NETCOREAPP3_0_OR_GREATER
        // Interface with a default implementation
        public interface IPrivateStruct
        {
            ValuesDuckType Value { get; }

            [DuckIgnore]
            public string GetValueProp => Value.ToString();

            [DuckIgnore]
            public string GetValue() => Value.ToString();
        }
#endif

        public abstract class AbstractPrivateProxy : IGetValue
        {
            public abstract ValuesDuckType Value { get; }

            [DuckIgnore]
            public string GetValueProp => Value.ToString();

            [DuckIgnore]
            public string GetValue() => Value.ToString();
        }

        public class VirtualPrivateProxy : IGetValue
        {
            public virtual ValuesDuckType Value { get; }

            [DuckIgnore]
            public string GetValueProp => Value.ToString();

            [DuckIgnore]
            public string GetValue() => Value.ToString();
        }

        private readonly struct PrivateStruct
        {
            public readonly Values Value => Values.Third;
        }

        public interface IGetValue
        {
            string GetValueProp { get; }

            string GetValue();
        }

        public enum ValuesDuckType
        {
            /// <summary>
            /// First
            /// </summary>
            First,

            /// <summary>
            /// Second
            /// </summary>
            Second,

            /// <summary>
            /// Third
            /// </summary>
            Third
        }

        public enum Values
        {
            /// <summary>
            /// First
            /// </summary>
            First,

            /// <summary>
            /// Second
            /// </summary>
            Second,

            /// <summary>
            /// Third
            /// </summary>
            Third
        }
    }
}
