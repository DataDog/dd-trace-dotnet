// <copyright file="SyntaxKindProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM.AST
{
    /// <summary>
    /// HotChocolate.Language.SyntaxKind interface for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/423e8a3285f4d20291bc78ce09fed23a091a01d0/src/HotChocolate/Language/src/Language.SyntaxTree/SyntaxKind.cs
    /// </summary>
    internal enum SyntaxKindProxy
    {
        Name,
        Document,
        OperationDefinition,
        VariableDefinition,
        Variable,
        SelectionSet,
        Field,
        Argument,
        FragmentSpread,
        InlineFragment,
        FragmentDefinition,
        IntValue,
        StringValue,
        BooleanValue,
        NullValue,
        EnumValue,
        ListValue,
        ObjectValue,
        ObjectField,
        Directive,
        NamedType,
        ListType,
        NonNullType,
        SchemaDefinition,
        OperationTypeDefinition,
        ScalarTypeDefinition,
        ObjectTypeDefinition,
        FieldDefinition,
        InputValueDefinition,
        InterfaceTypeDefinition,
        UnionTypeDefinition,
        EnumTypeDefinition,
        EnumValueDefinition,
        InputObjectTypeDefinition,
        SchemaExtension,
        ScalarTypeExtension,
        ObjectTypeExtension,
        InterfaceTypeExtension,
        UnionTypeExtension,
        EnumTypeExtension,
        InputObjectTypeExtension,
        DirectiveDefinition,
        FloatValue,
        ListNullability,
        RequiredModifier,
        OptionalModifier,
        SchemaCoordinate,
    }
}
