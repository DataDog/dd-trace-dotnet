// <copyright file="ASTNodeKindProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQLParser.AST.ASTNodeKind
/// </summary>
internal enum ASTNodeKindProxy
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
    FloatValue,
    StringValue,
    BooleanValue,
    EnumValue,
    ListValue,
    ObjectValue,
    ObjectField,
    Directive,
    NamedType,
    ListType,
    NonNullType,
    NullValue,
    SchemaDefinition,
    RootOperationTypeDefinition,
    ScalarTypeDefinition,
    ObjectTypeDefinition,
    FieldDefinition,
    InputValueDefinition,
    InterfaceTypeDefinition,
    UnionTypeDefinition,
    EnumTypeDefinition,
    EnumValueDefinition,
    InputObjectTypeDefinition,
    ObjectTypeExtension,
    DirectiveDefinition,
    Comment,
    Description,
    TypeCondition,
    Alias,
    ScalarTypeExtension,
    InterfaceTypeExtension,
    UnionTypeExtension,
    EnumTypeExtension,
    InputObjectTypeExtension,
    SchemaExtension,
    ArgumentsDefinition,
    Arguments,
    InputFieldsDefinition,
    VariablesDefinition,
    EnumValuesDefinition,
    FieldsDefinition,
    Directives,
    ImplementsInterfaces,
    DirectiveLocations,
    UnionMemberTypes,
    FragmentName,
}
