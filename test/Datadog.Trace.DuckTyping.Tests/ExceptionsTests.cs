using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class ExceptionsTests
    {
        [Fact]
        public void PropertyCantBeReadException()
        {
            object target = new PropertyCantBeReadExceptionClass();

            Assert.Throws<DuckTypePropertyCantBeReadException>(() =>
            {
                target.As<IPropertyCantBeReadException>();
            });
        }

        public interface IPropertyCantBeReadException
        {
            string OnlySetter { get; set; }
        }

        internal class PropertyCantBeReadExceptionClass
        {
            public string OnlySetter
            {
                set { }
            }
        }

        // *

        [Fact]
        public void PropertyCantBeWrittenException()
        {
            object target = new PropertyCantBeWrittenExceptionClass();

            Assert.Throws<DuckTypePropertyCantBeWrittenException>(() =>
            {
                target.As<IPropertyCantBeWrittenException>();
            });
        }

        public interface IPropertyCantBeWrittenException
        {
            string OnlyGetter { get; set; }
        }

        internal class PropertyCantBeWrittenExceptionClass
        {
            public string OnlyGetter { get; }
        }

        // *

        [Fact]
        public void PropertyArgumentsLengthException()
        {
            object target = new PropertyArgumentsLengthExceptionClass();

            Assert.Throws<DuckTypePropertyArgumentsLengthException>(() =>
            {
                target.As<IPropertyArgumentsLengthException>();
            });
        }

        public interface IPropertyArgumentsLengthException
        {
            string Item { get; }
        }

        internal class PropertyArgumentsLengthExceptionClass
        {
            public string this[string key]
            {
                get => null;
            }
        }

        // *

        [Fact]
        public void FieldIsReadonlyException()
        {
            object target = new FieldIsReadonlyExceptionClass();

            Assert.Throws<DuckTypeFieldIsReadonlyException>(() =>
            {
                target.As<IFieldIsReadonlyException>();
            });
        }

        public interface IFieldIsReadonlyException
        {
            [Duck(Name = "_name", Kind = DuckKind.Field)]
            string Name { get; set; }
        }

        internal class FieldIsReadonlyExceptionClass
        {
            private readonly string _name = string.Empty;

            public string AvoidCompileError => _name;
        }

        // *

        [Fact]
        public void PropertyOrFieldNotFoundException()
        {
            object target = new PropertyOrFieldNotFoundExceptionClass();

            Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
            {
                target.As<IPropertyOrFieldNotFoundException>();
            });
        }

        public interface IPropertyOrFieldNotFoundException
        {
            string Name { get; set; }
        }

        internal class PropertyOrFieldNotFoundExceptionClass
        {
        }

        // *

        [Fact]
        public void TypeIsNotPublicException()
        {
            object target = new TypeIsNotPublicExceptionClass();

            Assert.Throws<DuckTypeTypeIsNotPublicException>(() =>
            {
                target.As<ITypeIsNotPublicException>();
            });
        }

        internal interface ITypeIsNotPublicException
        {
            string Name { get; set; }
        }

        internal class TypeIsNotPublicExceptionClass
        {
            public string Name { get; set; }
        }

        // *

        [Fact]
        public void StructMembersCannotBeChangedException()
        {
            StructMembersCannotBeChangedExceptionStruct targetStruct = default;
            object target = (object)targetStruct;

            Assert.Throws<DuckTypeStructMembersCannotBeChangedException>(() =>
            {
                target.As<IStructMembersCannotBeChangedException>();
            });
        }

        public interface IStructMembersCannotBeChangedException
        {
            string Name { get; set; }
        }

        internal struct StructMembersCannotBeChangedExceptionStruct
        {
            public string Name { get; set; }
        }

        // *

        [Fact]
        public void TargetMethodNotFoundException()
        {
            object target = new TargetMethodNotFoundExceptionClass();

            Assert.Throws<DuckTypeTargetMethodNotFoundException>(() =>
            {
                target.As<ITargetMethodNotFoundException>();
            });
        }

        public interface ITargetMethodNotFoundException
        {
            public void AddTypo(string key, string value);
        }

        internal class TargetMethodNotFoundExceptionClass
        {
            public void Add(string key, string value)
            {
            }
        }

        // *

        [Fact]
        public void ProxyMethodParameterIsMissingException()
        {
            object target = new ProxyMethodParameterIsMissingExceptionClass();

            Assert.Throws<DuckTypeProxyMethodParameterIsMissingException>(() =>
            {
                target.As<IProxyMethodParameterIsMissingException>();
            });
        }

        public interface IProxyMethodParameterIsMissingException
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "System.String" })]
            public void Add(string key);
        }

        internal class ProxyMethodParameterIsMissingExceptionClass
        {
            public void Add(string key, string value)
            {
            }
        }

        // *

        [Fact]
        public void ProxyAndTargetMethodParameterSignatureMismatchException()
        {
            object target = new ProxyAndTargetMethodParameterSignatureMismatchExceptionClass();

            Assert.Throws<DuckTypeProxyAndTargetMethodParameterSignatureMismatchException>(() =>
            {
                target.As<IProxyAndTargetMethodParameterSignatureMismatchException>();
            });
        }

        public interface IProxyAndTargetMethodParameterSignatureMismatchException
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "System.String" })]
            public void Add(string key, ref string value);
        }

        internal class ProxyAndTargetMethodParameterSignatureMismatchExceptionClass
        {
            public void Add(string key, string value)
            {
            }
        }

        // *

        [Fact]
        public void ProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException()
        {
            object target = new ProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesExceptionClass();

            Assert.Throws<DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException>(() =>
            {
                target.As<IProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException>();
            });
        }

        public interface IProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException
        {
            public void Add<TKey, TValue>(TKey key, TValue value);
        }

        internal class ProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesExceptionClass
        {
            public void Add<TKey, TValue>(TKey key, TValue value)
            {
            }
        }

        // *

        [Fact]
        public void TargetMethodAmbiguousMatchException()
        {
            object target = new TargetMethodAmbiguousMatchExceptionClass();

            Assert.Throws<DuckTypeTargetMethodAmbiguousMatchException>(() =>
            {
                target.As<ITargetMethodAmbiguousMatchException>();
            });
        }

        public interface ITargetMethodAmbiguousMatchException
        {
            public void Add(string key, object value);

            public void Add(string key, string value);
        }

        internal class TargetMethodAmbiguousMatchExceptionClass
        {
            public void Add(string key, Task value)
            {
            }

            public void Add(string key, string value)
            {
            }
        }

        // *

        [Fact]
        public void ProxyTypeDefinitionIsNull()
        {
            Assert.Throws<DuckTypeProxyTypeDefinitionIsNull>(() =>
            {
                DuckType.Create(null, new object());
            });
        }

        // *

        [Fact]
        public void TargetObjectInstanceIsNull()
        {
            Assert.Throws<DuckTypeTargetObjectInstanceIsNull>(() =>
            {
                DuckType.Create(typeof(ITargetObjectInstanceIsNull), null);
            });
        }

        public interface ITargetObjectInstanceIsNull
        {
        }

        // *

        [Fact]
        public void InvalidTypeConversionException()
        {
            object target = new InvalidTypeConversionExceptionClass();

            Assert.Throws<DuckTypeInvalidTypeConversionException>(() =>
            {
                target.As<IInvalidTypeConversionException>();
            });
        }

        public interface IInvalidTypeConversionException
        {
            float Sum(int a, int b);
        }

        public class InvalidTypeConversionExceptionClass
        {
            public int Sum(int a, int b)
            {
                return a + b;
            }
        }
    }
}
