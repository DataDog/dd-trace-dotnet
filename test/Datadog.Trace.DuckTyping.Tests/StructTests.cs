using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class StructTests
    {
        [Fact]
        public void NonPublicStructCopyTest()
        {
            PrivateStruct instance = default;
            CopyStruct copy = instance.As<CopyStruct>();
            Assert.Equal(instance.Value, copy.Value);
        }

        [Fact]
        public void NonPublicStructInterfaceProxyTest()
        {
            PrivateStruct instance = default;
            IPrivateStruct proxy = instance.As<IPrivateStruct>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [Fact]
        public void NonPublicStructAbstractProxyTest()
        {
            PrivateStruct instance = default;
            AbstractPrivateProxy proxy = instance.As<AbstractPrivateProxy>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [Fact]
        public void NonPublicStructVirtualProxyTest()
        {
            PrivateStruct instance = default;
            VirtualPrivateProxy proxy = instance.As<VirtualPrivateProxy>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [DuckCopy]
        public struct CopyStruct
        {
            public string Value;
        }

        public interface IPrivateStruct
        {
            string Value { get; }
        }

        public abstract class AbstractPrivateProxy
        {
            public abstract string Value { get; }
        }

        public class VirtualPrivateProxy
        {
            public virtual string Value { get; }
        }

        private readonly struct PrivateStruct
        {
            public readonly string Value => "Hello World";
        }
    }
}
