// <copyright file="ExceptionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class ExceptionsTests
    {
        [Fact]
        public void PropertyCantBeReadException()
        {
            object target = new PropertyCantBeReadExceptionClass();

            Assert.Throws<DuckTypePropertyCantBeReadException>(() =>
            {
                target.DuckCast<IPropertyCantBeReadException>();
            });

            Assert.Throws<DuckTypePropertyCantBeReadException>(() =>
            {
                target.DuckCast<StructPropertyCantBeReadException>();
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
                target.DuckCast<IPropertyCantBeWrittenException>();
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
                target.DuckCast<IPropertyArgumentsLengthException>();
            });

            Assert.Throws<DuckTypePropertyArgumentsLengthException>(() =>
            {
                target.DuckCast<StructPropertyArgumentsLengthException>();
            });

            Assert.Throws<DuckTypePropertyArgumentsLengthException>(() =>
            {
                target.DuckCast<ISetPropertyArgumentsLengthException>();
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
                target.DuckCast<IFieldIsReadonlyException>();
            });
        }

        public interface IFieldIsReadonlyException
        {
            [DuckField(Name = "_name")]
            string Name { get; set; }
        }

        internal class FieldIsReadonlyExceptionClass
        {
            private readonly string _name = string.Empty;

            public string AvoidCompileError => _name;
        }

        // *

        [Theory]
        [InlineData(
            typeof(IProxyAndTargetMethodReturnTypeMismatchExceptionVoid),
            typeof(ProxyAndTargetMethodReturnTypeMismatchExceptionNonVoidClass))]
        [InlineData(
            typeof(IProxyAndTargetMethodReturnTypeMismatchExceptionNonVoid),
            typeof(ProxyAndTargetMethodReturnTypeMismatchExceptionVoidClass))]
        public void ProxyAndTargetMethodReturnTypeMismatchException(Type castTo, Type instanceType)
        {
            object target = Activator.CreateInstance(instanceType);
            Action cast = () => target.DuckCast(castTo);

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeProxyAndTargetMethodReturnTypeMismatchException>();
        }

        public interface IProxyAndTargetMethodReturnTypeMismatchExceptionVoid
        {
            void GetName();
        }

        public interface IProxyAndTargetMethodReturnTypeMismatchExceptionNonVoid
        {
            string GetName();
        }

        internal class ProxyAndTargetMethodReturnTypeMismatchExceptionNonVoidClass
        {
            public string GetName() => default;
        }

        internal class ProxyAndTargetMethodReturnTypeMismatchExceptionVoidClass
        {
            public void GetName()
            {
            }
        }

        // *

        [Theory]
        [InlineData(
            typeof(IReverseProxyAndTargetMethodReturnTypeMismatchExceptionVoid),
            typeof(ReverseProxyAndTargetMethodReturnTypeMismatchExceptionNonVoidClass))]
        [InlineData(
            typeof(IReverseProxyAndTargetMethodReturnTypeMismatchExceptionNonVoid),
            typeof(ReverseProxyAndTargetMethodReturnTypeMismatchExceptionVoidClass))]
        public void ReverseProxyAndTargetMethodReturnTypeMismatchException(Type typeToImplement, Type instanceType)
        {
            object target = Activator.CreateInstance(instanceType);
            Action cast = () => target.DuckImplement(typeToImplement);

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeProxyAndTargetMethodReturnTypeMismatchException>();
        }

        public interface IReverseProxyAndTargetMethodReturnTypeMismatchExceptionVoid
        {
            void GetName();
        }

        public interface IReverseProxyAndTargetMethodReturnTypeMismatchExceptionNonVoid
        {
            string GetName();
        }

        internal class ReverseProxyAndTargetMethodReturnTypeMismatchExceptionNonVoidClass
        {
            [DuckReverseMethod]
            public string GetName() => default;
        }

        internal class ReverseProxyAndTargetMethodReturnTypeMismatchExceptionVoidClass
        {
            [DuckReverseMethod]
            public void GetName()
            {
            }
        }

        // *

        [Fact]
        public void IncorrectReversePropertyUsageException()
        {
            object target = new IncorrectReversePropertyUsageExceptionClass();

            Assert.Throws<DuckTypeIncorrectReversePropertyUsageException>(() =>
            {
                target.DuckCast<IIncorrectReversePropertyUsageException>();
            });
        }

        public interface IIncorrectReversePropertyUsageException
        {
            [DuckReverseMethod]
            string Name { get; set; }
        }

        internal class IncorrectReversePropertyUsageExceptionClass
        {
            public string Name { get; set; }
        }

        // *

        [Fact]
        public void IncorrectReverseMethodUsageException()
        {
            object target = new IncorrectReverseMethodUsageExceptionClass();

            Assert.Throws<DuckTypeIncorrectReverseMethodUsageException>(() =>
            {
                target.DuckCast<IIncorrectReverseMethodUsageException>();
            });
        }

        public interface IIncorrectReverseMethodUsageException
        {
            [DuckReverseMethod]
            string GetName();
        }

        internal class IncorrectReverseMethodUsageExceptionClass
        {
            public string GetName() => default;
        }

        // *

        [Fact]
        public void ReverseProxyBaseIsStructException()
        {
            object target = new ReverseProxyBaseIsStructExceptionClass { Name = "Test" };

            Action cast = () => target.DuckImplement(typeof(ReverseProxyBaseIsStructExceptionBase));
            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseProxyBaseIsStructException>();
        }

        public struct ReverseProxyBaseIsStructExceptionBase
        {
            public string Name { get; set; }
        }

        internal class ReverseProxyBaseIsStructExceptionClass
        {
            [DuckReverseMethod]
            public string Name { get; set; }
        }

        // *

        [Fact]
        public void ReverseProxyImplementorIsAbstractOrInterfaceException()
        {
            Action cast = () => DuckType
                               .GetOrCreateReverseProxyType(
                                    typeToDeriveFrom: typeof(IReverseProxyImplementorIsAbstractOrInterfaceExceptionBase),
                                    delegationType: typeof(ReverseProxyImplementorIsAbstractOrInterfaceExceptionClass))
                               .CreateInstance(new object());

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException>();
        }

        public interface IReverseProxyImplementorIsAbstractOrInterfaceExceptionBase
        {
            public string Name { get; set; }
        }

        internal abstract class ReverseProxyImplementorIsAbstractOrInterfaceExceptionClass
        {
            [DuckReverseMethod]
            public abstract string Name { get; set; }
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
                    target.DuckCast<IPropertyOrFieldNotFoundException>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.DuckCast<IPropertyOrFieldNotFound2Exception>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.DuckCast<IPropertyOrFieldNotFound3Exception>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.DuckCast<PropertyOrFieldNotFoundExceptionStruct>();
                });

                Assert.Throws<DuckTypePropertyOrFieldNotFoundException>(() =>
                {
                    target.DuckCast<PropertyOrFieldNotFound2ExceptionStruct>();
                });
            }
        }

        public interface IPropertyOrFieldNotFoundException
        {
            string Name { get; set; }
        }

        public interface IPropertyOrFieldNotFound2Exception
        {
            [DuckField]
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
            [DuckField]
            public string Name;
        }

        internal class PropertyOrFieldNotFoundExceptionClass
        {
        }

        internal struct PropertyOrFieldNotFoundExceptionTargetStruct
        {
        }

        [Fact]
        public void StructMembersCannotBeChangedException()
        {
            StructMembersCannotBeChangedExceptionStruct targetStruct = default;
            object target = (object)targetStruct;

            Assert.Throws<DuckTypeStructMembersCannotBeChangedException>(() =>
            {
                target.DuckCast<IStructMembersCannotBeChangedException>();
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
                target.DuckCast<IStructMembersCannotBeChanged2Exception>();
            });
        }

        public interface IStructMembersCannotBeChanged2Exception
        {
            [DuckField]
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
                target.DuckCast<ITargetMethodNotFoundException>();
            });

            Assert.Throws<DuckTypeTargetMethodNotFoundException>(() =>
            {
                target.DuckCast<ITargetMethodNotFound2Exception>();
            });

            Assert.Throws<DuckTypeTargetMethodNotFoundException>(() =>
            {
                target.DuckCast<ITargetMethodNotFound3Exception>();
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
                target.DuckCast<IProxyMethodParameterIsMissingException>();
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
                target.DuckCast<IProxyAndTargetMethodParameterSignatureMismatchException>();
            });

            Assert.Throws<DuckTypeProxyAndTargetMethodParameterSignatureMismatchException>(() =>
            {
                target.DuckCast<IProxyAndTargetMethodParameterSignatureMismatch2Exception>();
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

        [Theory]
        [InlineData(typeof(ReverseProxyMustImplementGenericMethodAsGenericExceptionClass1))]
        [InlineData(typeof(ReverseProxyMustImplementGenericMethodAsGenericExceptionClass2))]
        [InlineData(typeof(ReverseProxyMustImplementGenericMethodAsGenericExceptionClass3))]
        public void ReverseProxyMustImplementGenericMethodAsGenericException(Type implementationType)
        {
            object target = Activator.CreateInstance(implementationType);
            Action cast = () => target.DuckImplement(typeof(IReverseProxyMustImplementGenericMethodAsGenericException));

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseProxyMustImplementGenericMethodAsGenericException>();
        }

        public interface IReverseProxyMustImplementGenericMethodAsGenericException
        {
            public void Add<TKey, TValue>(TKey key, TValue value);
        }

        public class ReverseProxyMustImplementGenericMethodAsGenericExceptionClass1
        {
            [DuckReverseMethod]
            public void Add<TKey>(TKey key, object value)
            {
            }
        }

        public class ReverseProxyMustImplementGenericMethodAsGenericExceptionClass2
        {
            [DuckReverseMethod]
            public void Add(string key, object value)
            {
            }
        }

        public class ReverseProxyMustImplementGenericMethodAsGenericExceptionClass3
        {
            [DuckReverseMethod(GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            public void Add(int key, string value)
            {
            }
        }

        // *

        [Fact]
        public void ReverseProxyMissingMethodImplementationException()
        {
            object target = new ReverseProxyMissingMethodImplementationExceptionClass();
            Action cast = () => target.DuckImplement(typeof(IReverseProxyMissingMethodImplementationException));

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseProxyMissingMethodImplementationException>();
        }

        public interface IReverseProxyMissingMethodImplementationException
        {
            public void Add(int value1, int value2);
        }

        public class ReverseProxyMissingMethodImplementationExceptionClass
        {
        }

        // *

        [Fact]
        public void TargetMethodAmbiguousMatchException()
        {
            object target = new TargetMethodAmbiguousMatchExceptionClass();

            Assert.Throws<DuckTypeTargetMethodAmbiguousMatchException>(() =>
            {
                target.DuckCast<ITargetMethodAmbiguousMatchException>();
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

        [Theory]
        [InlineData(typeof(ReverseAttributeParameterNamesMismatchExceptionClass1))]
        [InlineData(typeof(ReverseAttributeParameterNamesMismatchExceptionClass2))]
        public void ReverseAttributeParameterNamesMismatchException(Type duckType)
        {
            object target = Activator.CreateInstance(duckType);

            Action cast = () => target.DuckImplement(typeof(IReverseAttributeParameterNamesMismatchException));

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseAttributeParameterNamesMismatchException>();
        }

        public interface IReverseAttributeParameterNamesMismatchException
        {
            public void Add(string key, string value);
        }

        public class ReverseAttributeParameterNamesMismatchExceptionClass1
        {
            [DuckReverseMethod(ParameterTypeNames = new[] { "System.String", "System.String", "System.String" })]
            public virtual void Add(string key, string value)
            {
            }
        }

        public class ReverseAttributeParameterNamesMismatchExceptionClass2
        {
            [DuckReverseMethod(ParameterTypeNames = new[] { "System.String", })]
            public virtual void Add(string key, string value)
            {
            }
        }

        [Fact]
        public void NonSpecificExceptionDuringBuildingThrowsDuckTypeException()
        {
            object target = new ReverseProxyMissingPropertyImplementationExceptionClass();
            Action cast = () => target.DuckImplement(typeof(IReverseProxyMissingPropertyImplementationException));

            cast.Should()
                .Throw<TargetInvocationException>()
                .WithInnerExceptionExactly<DuckTypeReverseProxyMissingPropertyImplementationException>();
        }

        public interface IReverseProxyMissingPropertyImplementationException
        {
            string Value { get; set; }
        }

        public class ReverseProxyMissingPropertyImplementationExceptionClass
        {
        }

        // *

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

        [Fact]
        public void TargetObjectInstanceIsNullWithTryDuckCast()
        {
            DuckTypeExtensions.TryDuckCast(null, out ITargetObjectInstanceIsNull value).Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithTryDuckCast2()
        {
            DuckTypeExtensions.TryDuckCast(null, typeof(ITargetObjectInstanceIsNull), out var value).Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithDuckAs()
        {
            DuckTypeExtensions.DuckAs<ITargetObjectInstanceIsNull>(null).Should().BeNull();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithDuckAs2()
        {
            DuckTypeExtensions.DuckAs(null, typeof(ITargetObjectInstanceIsNull)).Should().BeNull();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithDuckIs()
        {
            DuckTypeExtensions.DuckIs<ITargetObjectInstanceIsNull>(null).Should().BeFalse();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithDuckIs2()
        {
            DuckTypeExtensions.DuckIs(null, typeof(ITargetObjectInstanceIsNull)).Should().BeFalse();
        }

        [Fact]
        public void TargetObjectInstanceIsNullWithTryDuckImplement()
        {
            DuckTypeExtensions.TryDuckImplement(null, typeof(ITargetObjectInstanceIsNull), out var value).Should().BeFalse();
            value.Should().BeNull();
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
                target.DuckCast<IInvalidTypeConversionException>();
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
                target.DuckCast<IObjectInvalidTypeConversionException>();
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
                target.DuckCast<IObjectInvalidTypeConversion2Exception>();
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
                target.DuckCast<IObjectInvalidTypeConversion3Exception>();
            });
        }

        public interface IObjectInvalidTypeConversion3Exception
        {
            [DuckField]
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

        // *
        [Fact]
        public void DuckCopyContainsNoFieldsException()
        {
            object target = new SourceObject();

            Assert.Throws<DuckTypeDuckCopyStructDoesNotContainsAnyField>(() =>
            {
                target.DuckCast<SourceStructProxy>();
            });
        }

        [DuckCopy]
        public struct SourceStructProxy
        {
            public string Name { get; set; }
        }

        public class SourceObject
        {
            public string Name { get; set; }
        }
    }
}
