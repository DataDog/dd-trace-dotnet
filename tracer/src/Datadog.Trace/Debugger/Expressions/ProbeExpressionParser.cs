// <copyright file="ProbeExpressionParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using static Datadog.Trace.Debugger.Expressions.ProbeExpressionParserHelper;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private static readonly LabelTarget ReturnTarget = Expression.Label(typeof(T));

    /// <summary>
    /// This, Return, Exception, LocalsAndArgs
    /// </summary>
    private static readonly Func<ScopeMember, ScopeMember, Exception, ScopeMember[], T> DefaultDelegate;

    private List<EvaluationError> _errors;
    private int _arrayStack;

    static ProbeExpressionParser()
    {
        DefaultDelegate = (_, _, _, _) =>
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)true;
            }

            return default;
        };
    }

    private delegate Expression Combiner(Expression left, Expression right);

    private Expression ParseRoot(
        JsonTextReader reader,
        List<ParameterExpression> parameters)
    {
        var readerValue = reader.Value?.ToString();
        switch (reader.TokenType)
        {
            case JsonToken.PropertyName:
                switch (readerValue)
                {
                    case "and":
                        {
                            return ConditionalOperator(reader, Expression.AndAlso, parameters);
                        }

                    case "or":
                        {
                            return ConditionalOperator(reader, Expression.OrElse, parameters);
                        }

                    default:
                        return ParseTree(reader, parameters, null, false);
                }
        }

        return null;
    }

    private Expression ConditionalOperator(JsonTextReader reader, Combiner combiner, List<ParameterExpression> parameters)
    {
        Expression Combine(Expression leftOperand, Expression rightOperand) =>
            leftOperand == null ? rightOperand : combiner(leftOperand, rightOperand.Type != ProbeExpressionParserHelper.UndefinedValueType ? rightOperand : Expression.Constant(true));

        _arrayStack++;
        reader.Read();
        var right = ParseTree(reader, parameters, null);
        var left = Combine(null, right);

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                _arrayStack++;
                continue;
            }
            else if (reader.TokenType == JsonToken.EndArray)
            {
                _arrayStack--;
                continue;
            }

            if (reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
            {
                continue;
            }

            if (_arrayStack == 0)
            {
                break;
            }

            right = ParseTree(reader, parameters, null, false);
            left = Combine(left, right);
        }

        return left;
    }

    private Expression ParseTree(
        JsonTextReader reader,
        List<ParameterExpression> parameters,
        ParameterExpression itParameter,
        bool shouldAdvanceReader = true)
    {
        int safeguard = 0;
        const int maxIteration = 100000;
        while (ConditionalRead(reader, shouldAdvanceReader) && safeguard++ <= maxIteration)
        {
            var readerValue = reader.Value?.ToString();
            try
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        {
                            switch (readerValue)
                            {
                                // operators
                                case "and":
                                case "&&":
                                    {
                                        var right = ParseRoot(reader, parameters);
                                        return right;
                                    }

                                case "or":
                                case "||":
                                    {
                                        var right = ParseRoot(reader, parameters);
                                        return right;
                                    }

                                case "eq":
                                case "==":
                                    {
                                        return Equal(reader, parameters, itParameter);
                                    }

                                case "!=":
                                case "neq":
                                    {
                                        return NotEqual(reader, parameters, itParameter);
                                    }

                                case ">":
                                case "gt":
                                    {
                                        return GreaterThan(reader, parameters, itParameter);
                                    }

                                case ">=":
                                case "ge":
                                    {
                                        return GreaterThanOrEqual(reader, parameters, itParameter);
                                    }

                                case "<":
                                case "lt":
                                    {
                                        return LessThan(reader, parameters, itParameter);
                                    }

                                case "<=":
                                case "le":
                                    {
                                        return LessThanOrEqual(reader, parameters, itParameter);
                                    }

                                case "not":
                                case "!":
                                    {
                                        return Not(reader, parameters, itParameter);
                                    }

                                // string operations
                                case "isEmpty":
                                    {
                                        return IsEmpty(reader, parameters, itParameter);
                                    }

                                case "len":
                                    {
                                        return Length(reader, parameters, itParameter);
                                    }

                                case "substring":
                                    {
                                        return Substring(reader, parameters, itParameter);
                                    }

                                case "startWith":
                                    {
                                        return StartWith(reader, parameters, itParameter);
                                    }

                                case "endWith":
                                    {
                                        return EndWith(reader, parameters, itParameter);
                                    }

                                case "contains":
                                    {
                                        return Contains(reader, parameters, itParameter);
                                    }

                                case "matches":
                                    {
                                        return RegexMatches(reader, parameters, itParameter);
                                    }

                                // collection operations
                                case "hasAny":
                                    {
                                        return HasAny(reader, parameters);
                                    }

                                case "hasAll":
                                    {
                                        return HasAll(reader, parameters);
                                    }

                                case "filter":
                                    {
                                        return Filter(reader, parameters);
                                    }

                                case "count":
                                    {
                                        return Count(reader, parameters, itParameter);
                                    }

                                case "index":
                                    {
                                        return GetItemAtIndex(reader, parameters, itParameter);
                                    }

                                // generic operations
                                case "getmember":
                                    {
                                        return GetMember(reader, parameters, itParameter);
                                    }

                                case "ref":
                                    {
                                        return GetReference(reader, parameters, itParameter);
                                    }

                                case "isUndefined":
                                    {
                                        return IsUndefined(reader, parameters, itParameter);
                                    }

                                case "Ignore":
                                case "ignore":
                                    {
                                        reader.Read();
                                        break;
                                    }

                                default:
                                    {
                                        AddError(readerValue, "Operator has not defined");
                                        return ReturnDefaultValueExpression();
                                    }
                            }

                            break;
                        }

                    case JsonToken.String:
                        {
                            if (readerValue?.StartsWith("#") == true)
                            {
                                // skip comment
                                return ParseTree(reader, parameters, itParameter);
                            }

                            if (readerValue == "@return")
                            {
                                return GetParameterExpression(parameters, ScopeMemberKind.Return);
                            }

                            if (readerValue == "@exceptions")
                            {
                                return GetParameterExpression(parameters, ScopeMemberKind.Exception);
                            }

                            if (readerValue == "@duration")
                            {
                                return Expression.Constant("@duration is not yet supported");
                            }

                            if (readerValue == "@it")
                            {
                                // current item in iterator
                                if (itParameter == null)
                                {
                                    AddError(readerValue, "current item in iterator is null");
                                    return Expression.Parameter(UndefinedValueType, Expressions.UndefinedValue.Instance.ToString());
                                }

                                return itParameter;
                            }

                            return Expression.Constant(readerValue);
                        }

                    case JsonToken.Integer:
                        {
                            return Expression.Constant(Convert.ChangeType(readerValue, TypeCode.Int32));
                        }

                    case JsonToken.StartArray:
                        {
                            _arrayStack++;
                            break;
                        }

                    case JsonToken.EndArray:
                        {
                            _arrayStack--;
                            break;
                        }

                    case JsonToken.Null:
                        {
                            return Expression.Constant(null);
                        }
                }
            }
            catch (Exception e)
            {
                AddError(reader.Value?.ToString() ?? "N/A", e.Message);
                return ReturnDefaultValueExpression();
            }
        }

        if (safeguard >= maxIteration)
        {
            throw new ArgumentException("Invalid json file", nameof(reader));
        }

        return null;
    }

    private void AddError(string expression, string error)
    {
        (_errors ??= new List<EvaluationError>()).Add(new EvaluationError { Expression = expression, Message = error });
    }

    private bool ConditionalRead(JsonTextReader reader, bool shouldAdvanceReader)
    {
        return !shouldAdvanceReader || reader.Read();
    }

    private void SetReaderAtExpressionStart(JsonTextReader reader)
    {
        int safeguard = 0;
        const int maxIteration = 100;
        while (reader.Read() && safeguard++ <= maxIteration)
        {
            if (reader.TokenType != JsonToken.PropertyName)
            {
                continue;
            }

            return;
        }

        if (safeguard >= maxIteration)
        {
            throw new ArgumentException("Invalid json file", nameof(reader));
        }
    }

    private Expression HandleReturnType(Expression finalExpr)
    {
        if (typeof(T).IsAssignableFrom(finalExpr.Type))
        {
            return finalExpr;
        }

        if (typeof(T) != typeof(string))
        {
            // let the caller throw the correct exception
            return finalExpr;
        }

        // for string, call ToString when possible, build exception message or return the type name
        if (SupportedTypesService.IsSafeToCallToString(finalExpr.Type))
        {
            finalExpr = Expression.Call(finalExpr, GetMethodByReflection(typeof(object), nameof(object.ToString), Type.EmptyTypes));
        }
        else if (IsMicrosoftException(finalExpr.Type))
        {
            var stringConcat = GetMethodByReflection(typeof(string), nameof(string.Concat), new[] { typeof(object[]) });
            var typeNameExpression = Expression.Constant(finalExpr.Type.FullName, typeof(string));
            var ifNull = Expression.Equal(finalExpr, Expression.Constant(null));
            var exceptionAsString = Expression.Call(
                stringConcat,
                Expression.NewArrayInit(
                    typeof(string),
                    Expression.Constant(finalExpr.Type.FullName, typeof(string)),
                    Expression.Constant(Environment.NewLine, typeof(string)),
                    Expression.Property(finalExpr, nameof(Exception.Message)),
                    Expression.Constant(Environment.NewLine, typeof(string)),
                    Expression.Property(finalExpr, nameof(Exception.StackTrace))));
            return Expression.Condition(ifNull, typeNameExpression, exceptionAsString);
        }
        else
        {
            finalExpr = Expression.Constant(finalExpr.Type.FullName, typeof(string));
        }

        return finalExpr;
    }

    private void AddLocalAndArgs(ScopeMember[] argsOrLocals, List<ParameterExpression> scopeMembers, List<Expression> expressions, ParameterExpression argsOrLocalsParameterExpression)
    {
        for (var index = 0; index < argsOrLocals.Length; index++)
        {
            if (argsOrLocals[index].Type == null)
            {
                // ArrayPool can allocate more items than we need, if "Type == null", this mean we can exit the loop because Type should never be null
                break;
            }

            var argOrLocal = argsOrLocals[index];
            var variable = Expression.Variable(argOrLocal.Type, argOrLocal.Name);
            scopeMembers.Add(variable);

            expressions.Add(
                Expression.Assign(
                    variable,
                    Expression.Convert(
                        Expression.Field(
                            Expression.ArrayIndex(
                                argsOrLocalsParameterExpression,
                                Expression.Constant(index)),
                            "Value"),
                        argOrLocal.Type)));
        }
    }

    private ExpressionBodyAndParameters ParseProbeExpression(string expressionJson, MethodScopeMembers methodScopeMembers)
    {
        var argsOrLocals = methodScopeMembers.Members;
        var @this = methodScopeMembers.InvocationTarget;
        var thisType = @this.Type ?? @this.Value?.GetType();
        if (string.IsNullOrEmpty(expressionJson) || argsOrLocals == null || thisType == null)
        {
            throw new ArgumentException($"{nameof(ParseProbeExpression)} has been called with an invalid argument");
        }

        var scopeMembers = new List<ParameterExpression>();
        var expressions = new List<Expression>();

        // Add 'this'
        var thisParameterExpression = Expression.Parameter(@this.GetType());
        var thisVariable = Expression.Variable(thisType, "this");
        expressions.Add(Expression.Assign(thisVariable, Expression.Convert(Expression.Field(thisParameterExpression, "Value"), thisType)));
        scopeMembers.Add(thisVariable);

        // add 'return'
        var @return = methodScopeMembers.Return;
        var returnType = @return.Type ?? @return.Value?.GetType();
        ParameterExpression returnVariable;
        var returnParameterExpression = Expression.Parameter(@return.GetType());
        if (returnType == null || returnType == typeof(void))
        {
            returnVariable = Expression.Variable(typeof(string), "@return");
            expressions.Add(Expression.Assign(returnVariable, Expression.Constant($"Return type is {typeof(void).FullName}")));
        }
        else
        {
            returnVariable = Expression.Variable(returnType, "@return");
            expressions.Add(Expression.Assign(returnVariable, Expression.Convert(Expression.Field(returnParameterExpression, "Value"), returnType)));
        }

        scopeMembers.Add(returnVariable);

        // add exception
        var exceptionParameterExpression = Expression.Parameter(typeof(Exception));
        var exceptionVariable = Expression.Variable(typeof(Exception), "@exception");
        expressions.Add(Expression.Assign(exceptionVariable, exceptionParameterExpression));
        scopeMembers.Add(exceptionVariable);

        // add args and locals
        var argsOrLocalsParameterExpression = Expression.Parameter(argsOrLocals.GetType());
        AddLocalAndArgs(argsOrLocals, scopeMembers, expressions, argsOrLocalsParameterExpression);

        var result = Expression.Variable(typeof(T), "$dd_el_result");
        scopeMembers.Add(result);

        var reader = new JsonTextReader(new StringReader(expressionJson));
        SetReaderAtExpressionStart(reader);

        var finalExpr = ParseRoot(reader, scopeMembers);
        finalExpr = HandleReturnType(finalExpr);
        expressions.Add(finalExpr is not GotoExpression ? Expression.Assign(result, finalExpr) : finalExpr);
        expressions.Add(Expression.Label(ReturnTarget, result));
        var body = (Expression)Expression.Block(scopeMembers, expressions);
        if (body.CanReduce)
        {
            body = body.ReduceAndCheck();
        }

        return new ExpressionBodyAndParameters(body, thisParameterExpression, returnParameterExpression, exceptionParameterExpression, argsOrLocalsParameterExpression);
    }

    internal static CompiledExpression<T> ParseExpression(JObject expressionJson, MethodScopeMembers scopeMembers)
    {
        return ParseExpression(expressionJson.ToString(), scopeMembers);
    }

    internal static CompiledExpression<T> ParseExpression(string expressionJson, MethodScopeMembers scopeMembers)
    {
        var parser = new ProbeExpressionParser<T>();
        ExpressionBodyAndParameters parsedExpression = default;
        try
        {
            parsedExpression = parser.ParseProbeExpression(expressionJson, scopeMembers);
            var expression = Expression.Lambda<Func<ScopeMember, ScopeMember, Exception, ScopeMember[], T>>(parsedExpression.ExpressionBody, parsedExpression.ThisParameterExpression, parsedExpression.ReturnParameterExpression, parsedExpression.ExceptionParameterExpression, parsedExpression.ArgsAndLocalsParameterExpression);
            var compiled = expression.Compile();
            return new CompiledExpression<T>(compiled, expression, expressionJson, parser._errors?.ToArray());
        }
        catch (Exception e)
        {
            parser.AddError(parsedExpression.ExpressionBody?.ToString() ?? expressionJson, e.Message);
            return new CompiledExpression<T>(
                DefaultDelegate,
                parsedExpression.ExpressionBody ?? Expression.Constant("N/A"),
                expressionJson,
                parser._errors.ToArray());
        }
    }
}
