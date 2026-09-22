// <copyright file="CompiledExpressionDelegate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Debugger.Models;

namespace Datadog.Trace.Debugger.Expressions;

internal delegate T CompiledExpressionDelegate<T>(
    ScopeMember invocationTarget,
    ScopeMember returnValue,
    ScopeMember duration,
    Exception exception,
    ScopeMember[] members,
    ref EvaluationBudget budget);
