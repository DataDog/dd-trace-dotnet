// <copyright file="GlobalSuppressions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("StyleCop.CSharp.SpecialRules", "SA0001:XmlCommentAnalysisDisabled", Justification = "Documentation is not generated in the CI.")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1119:StatementMustNotUseUnnecessaryParenthesis", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1120:Comments should contain text", Justification = "Allow more readable separation in comments")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1121:Use built-in type alias", Justification = "infixable cases without real consistent value")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1131:Use readable conditions", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1200:UsingDirectivesMustBePlacedWithinNamespace", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:FieldNamesMustNotBeginWithUnderscore", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1413:UseTrailingCommasInMultiLineInitializers", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1501:Statement must not be on a single line", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1512:Single-line comments must not be followed by blank line", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment must be preceded by blank line", Justification = "Reviewed.")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "Easier to group single line helper members")]
