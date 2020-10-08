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

            Assert.Throws<DuckTypePropertyCantBeReadException>(() =>
            {
                target.As<StructPropertyCantBeReadException>();
            });
        }

        public interface IPropertyCantBeReadException
        {
            string OnlySetter { get; set; }
        }

        public struct StructPropertyCantBeReadException
        {
            public string OnlySetter;
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

            Assert.Throws<DuckTypePropertyArgumentsLengthException>(() =>
            {
                target.As<StructPropertyArgumentsLengthException>();
            });

            Assert.Throws<DuckTypePropertyArgumentsLengthException>(() =>
            {
                target.As<ISetPropertyArgumentsLengthException>();
            });
        }

        public interface IPropertyArgumentsLengthException
        {
            string Item { get; }
        }

        [DuckCopy]
        public struct StructPropertyArgumentsLengthException
        {
            public string Item;
        }

        public interface ISetPropertyArgumentsLengthException
        {
            string Item { set; }
        }

        internal class PropertyArgumentsLengthExceptionClass
        {
            public string this[string key]
            {
                get => null;
                set { }
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
            object[] targets = new object[]
            {
                new PropertyOrFieldNotFoundExceptionClass(),
                (PropertyOrFieldNotFoundExceptionTargetStruct)default
            };

            foreach (object target in targets)
            {
                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.As<IPropertyOrFieldNotFoundException>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.As<IPropertyOrFieldNotFound2Exception>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.As<IPropertyOrFieldNotFound3Exception>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.As<PropertyOrFieldNotFoundExceptionStruct>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.As<PropertyOrFieldNotFound2ExceptionStruct>();
                });
            }
        }

        public interface IPropertyOrFieldNotFoundException
        {
            string Name { get; set; }
        }

        public interface IPropertyOrFieldNotFound2Exception
        {
            [Duck(Kind = DuckKind.Field)]
            string Name { get; set; }
        }

        public interface IPropertyOrFieldNotFound3Exception
        {
            string Name { set; }
        }

        public struct PropertyOrFieldNotFoundExceptionStruct
        {
            public string Name;
        }

        public struct PropertyOrFieldNotFound2ExceptionStruct
        {
            [Duck(Kind = DuckKind.Field)]
            public string Name;
        }

        internal class PropertyOrFieldNotFoundExceptionClass
        {
        }

        internal struct PropertyOrFieldNotFoundExceptionTargetStruct
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

            Assert.Throws<DuckTypeTypeIsNotPublicException>(() =>
            {
                target.As(typeof(ITypeIsNotPublicException));
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
        public void StructMembersCannotBeChanged2Exception()
        {
            StructMembersCannotBeChanged2ExceptionStruct targetStruct = default;
            object target = (object)targetStruct;

            Assert.Throws<DuckTypeStructMembersCannotBeChangedException>(() =>
            {
                target.As<IStructMembersCannotBeChanged2Exception>();
            });
        }

        public interface IStructMembersCannotBeChanged2Exception
        {
            [Duck(Kind = DuckKind.Field)]
            string Name { get; set; }
        }

        internal struct StructMembersCannotBeChanged2ExceptionStruct
        {
#pragma warning disable 649
            public string Name;
#pragma warning restore 649
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

            Assert.Throws<DuckTypeTargetMethodNotFoundException>(() =>
            {
                target.As<ITargetMethodNotFound2Exception>();
            });

            Assert.Throws<DuckTypeTargetMethodNotFoundException>(() =>
            {
                target.As<ITargetMethodNotFound3Exception>();
            });
        }

        public interface ITargetMethodNotFoundException
        {
            public void AddTypo(string key, string value);
        }

        public interface ITargetMethodNotFound2Exception
        {
            public void AddGeneric(object value);
        }

        public interface ITargetMethodNotFound3Exception
        {
            [Duck(GenericParameterTypeNames = new string[] { "P1", "P2" })]
            public void AddGeneric(object value);
        }

        internal class TargetMethodNotFoundExceptionClass
        {
            public void Add(string key, string value)
            {
            }

            public void AddGeneric<T>(T value)
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

            Assert.Throws<DuckTypeProxyAndTargetMethodParameterSignatureMismatchException>(() =>
            {
                target.As<IProxyAndTargetMethodParameterSignatureMismatch2Exception>();
            });
        }

        public interface IProxyAndTargetMethodParameterSignatureMismatchException
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "System.String" })]
            public void Add(string key, ref string value);
        }

        public interface IProxyAndTargetMethodParameterSignatureMismatch2Exception
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "System.String" })]
            public void Add(string key, out string value);
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

        // *

        [Fact]
        public void ObjectInvalidTypeConversionException()
        {
            object target = new ObjectInvalidTypeConversionExceptionClass();

            Assert.Throws<DuckTypeInvalidTypeConversionException>(() =>
            {
                target.As<IObjectInvalidTypeConversionException>();
            });
        }

        public interface IObjectInvalidTypeConversionException
        {
            string Value { get; }
        }

        public class ObjectInvalidTypeConversionExceptionClass
        {
            public int Value => 42;
        }

        // *

        [Fact]
        public void ObjectInvalidTypeConversion2Exception()
        {
            object target = new ObjectInvalidTypeConversion2ExceptionClass();

            Assert.Throws<DuckTypeInvalidTypeConversionException>(() =>
            {
                target.As<IObjectInvalidTypeConversion2Exception>();
            });
        }

        public interface IObjectInvalidTypeConversion2Exception
        {
            int Value { get; }
        }

        public class ObjectInvalidTypeConversion2ExceptionClass
        {
            public string Value => "Hello world";
        }

        // *

        [Fact]
        public void ObjectInvalidTypeConversion3Exception()
        {
            object target = new ObjectInvalidTypeConversion3ExceptionClass();

            Assert.Throws<DuckTypeInvalidTypeConversionException>(() =>
            {
                target.As<IObjectInvalidTypeConversion3Exception>();
            });
        }

        public interface IObjectInvalidTypeConversion3Exception
        {
            [Duck(Kind = DuckKind.Field)]
            int Value { get; }
        }

        public class ObjectInvalidTypeConversion3ExceptionClass
        {
#pragma warning disable 414
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable SA1306 // Field names must begin with lower-case letter
            private readonly string Value = "Hello world";
#pragma warning restore SA1306 // Field names must begin with lower-case letter
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore 414
        }
    }
}
