// <copyright file="ILToInlineILConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UpdateVendors
{
    /// <summary>
    /// Converts System.Runtime.CompilerServices.Unsafe.il (ILAsm) into an InlineIL-based C# file.
    /// This is a purpose-built converter for the specific patterns used in the Unsafe IL file;
    /// it is NOT a general-purpose IL-to-C# converter.
    /// </summary>
    public static class ILToInlineILConverter
    {
        private const string Indent = "        ";

        // IL type → C# type
        private static readonly Dictionary<string, string> TypeMap = new(StringComparer.Ordinal)
        {
            ["void*"] = "void*",
            ["void"] = "void",
            ["int32"] = "int",
            ["uint32"] = "uint",
            ["uint8"] = "byte",
            ["uint8&"] = "ref byte",
            ["native int"] = "IntPtr",
            ["native uint"] = "nuint",
            ["bool"] = "bool",
            ["object"] = "object",
        };

        /// <summary>
        /// Converts the content of a System.Runtime.CompilerServices.Unsafe.il file
        /// into a C# file using InlineIL. The output uses the original (non-vendored)
        /// namespaces; standard vendoring transforms (namespace rewriting, public→internal)
        /// are applied separately.
        /// </summary>
        public static string Convert(string ilContent)
        {
            var lines = ilContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var methods = ParseMethods(lines);
            return EmitCSharpFile(methods);
        }

        private static List<MethodInfo> ParseMethods(IEnumerable<string> lines)
        {
            var methods = new List<MethodInfo>();
            var state = ParserState.Preamble;
            var signatureAccumulator = "";
            MethodInfo currentMethod = null;
            var skippingMultiLineDirective = false;
            var pendingParamIndex = -1; // tracks .param [N] for IsReadOnlyAttribute detection

            foreach (var t in lines)
            {
                var trimmed = t.Trim();

                // Handle multi-line .custom / .param directives: skip continuation lines
                // until we find the closing ')'. These look like:
                //   .custom instance void ...Attribute::.ctor() = (
                //       01 00 00 00
                //   )
                if (skippingMultiLineDirective)
                {
                    if (trimmed == ")" || trimmed.EndsWith(')'))
                    {
                        skippingMultiLineDirective = false;
                    }
                    continue;
                }

                switch (state)
                {
                    case ParserState.Preamble:
                        if (trimmed.StartsWith(".class") && trimmed.Contains("System.Runtime.CompilerServices.Unsafe"))
                        {
                            // Skip until we find the opening brace
                            state = ParserState.WaitingForClassOpen;
                        }
                        break;

                    case ParserState.WaitingForClassOpen:
                        if (trimmed == "{")
                        {
                            state = ParserState.ClassBody;
                        }
                        break;

                    case ParserState.ClassBody:
                        if (trimmed.StartsWith(".method"))
                        {
                            signatureAccumulator = trimmed;
                            if (trimmed.Contains("cil managed aggressiveinlining"))
                            {
                                currentMethod = ParseMethodSignature(signatureAccumulator);
                                state = ParserState.WaitingForMethodOpen;
                            }
                            else
                            {
                                state = ParserState.MethodSignature;
                            }
                        }
                        else if (trimmed.StartsWith("} // end of class"))
                        {
                            // Done with the main Unsafe class
                            state = ParserState.Done;
                        }
                        break;

                    case ParserState.MethodSignature:
                        signatureAccumulator += " " + trimmed;
                        if (signatureAccumulator.Contains("cil managed aggressiveinlining"))
                        {
                            currentMethod = ParseMethodSignature(signatureAccumulator);
                            state = ParserState.WaitingForMethodOpen;
                        }
                        break;

                    case ParserState.WaitingForMethodOpen:
                        if (trimmed == "{")
                        {
                            state = ParserState.MethodBody;
                        }
                        break;

                    case ParserState.MethodBody:
                        if (trimmed.StartsWith("} // end of method"))
                        {
                            methods.Add(currentMethod);
                            currentMethod = null;
                            state = ParserState.ClassBody;
                        }
                        else if (trimmed.StartsWith(".custom"))
                        {
                            // Check if this .custom attribute is IsReadOnlyAttribute
                            // for a pending .param directive
                            if (pendingParamIndex >= 0 && trimmed.Contains("IsReadOnlyAttribute"))
                            {
                                // Mark this parameter as 'in' instead of 'ref'
                                var param = currentMethod.Parameters[pendingParamIndex - 1]; // .param uses 1-based index
                                if (param.CsType.StartsWith("ref "))
                                {
                                    param.CsType = "in " + param.CsType.Substring("ref ".Length);
                                }
                            }

                            pendingParamIndex = -1;

                            // Skip multi-line custom attribute data
                            if (IsMultiLineDirectiveStart(trimmed))
                            {
                                skippingMultiLineDirective = true;
                            }
                        }
                        else if (trimmed.StartsWith(".param"))
                        {
                            // Track which parameter is being annotated (.param [N] uses 1-based index)
                            var paramMatch = Regex.Match(trimmed, @"\.param\s+\[(\d+)\]");
                            pendingParamIndex = paramMatch.Success ? int.Parse(paramMatch.Groups[1].Value) : -1;

                            // Skip multi-line data if present
                            if (IsMultiLineDirectiveStart(trimmed))
                            {
                                skippingMultiLineDirective = true;
                            }
                        }
                        else if (trimmed.StartsWith(".maxstack"))
                        {
                            var match = Regex.Match(trimmed, @"\.maxstack\s+(\d+)");
                            if (match.Success)
                            {
                                currentMethod.MaxStack = int.Parse(match.Groups[1].Value);
                            }
                        }
                        else if (trimmed.StartsWith(".locals"))
                        {
                            currentMethod.HasLocals = true;
                        }
                        else if (trimmed == "ret")
                        {
                            // Handled by return statement generation, not emitted as an instruction
                        }
                        else if (trimmed == "" || trimmed.StartsWith("//"))
                        {
                            // Skip blank lines and comments
                        }
                        else
                        {
                            // IL instruction
                            currentMethod.IlInstructions.Add(trimmed);
                        }
                        break;

                }

                if (state == ParserState.Done)
                {
                    break;
                }
            }

            return methods;
        }

        /// <summary>
        /// Checks if a directive line starts a multi-line block (contains '(' without a matching ')').
        /// For example: .custom instance void ...Attribute::.ctor() = (
        /// The '()' in .ctor() is balanced, so we check if there's an unbalanced '(' at the end.
        /// </summary>
        private static bool IsMultiLineDirectiveStart(string trimmed)
        {
            // Quick check: if the line ends with '(' and there's no matching ')' after the '= ('
            // pattern, it's multi-line.
            var eqParen = trimmed.LastIndexOf("= (", StringComparison.Ordinal);
            if (eqParen < 0)
                return false;

            // Check if there's a closing ')' after "= ("
            var afterEqParen = trimmed.Substring(eqParen + 3);
            return !afterEqParen.Contains(')');
        }

        private static MethodInfo ParseMethodSignature(string fullSignature)
        {
            // Normalize whitespace
            fullSignature = Regex.Replace(fullSignature.Trim(), @"\s+", " ");

            // Strip prefix and suffix
            const string prefix = ".method public hidebysig static ";
            const string suffix = " cil managed aggressiveinlining";

            var body = fullSignature;
            if (body.StartsWith(prefix))
                body = body.Substring(prefix.Length);
            var suffixIdx = body.IndexOf(suffix, StringComparison.Ordinal);
            if (suffixIdx >= 0)
                body = body.Substring(0, suffixIdx);

            // body is now something like:
            //   "!!T Read<T>(void* source)"
            //   "!!T& Add<T>(!!T& source, native int elementOffset)"
            //   "!!T& Unbox<valuetype .ctor ([CORE_ASSEMBLY]System.ValueType) T> (object 'box')"

            // Split at the opening parenthesis for the parameter list.
            // We need to find the '(' that starts the parameter list, not one inside generic constraints.
            // The parameter list '(' comes after the '>' of generics (if any), or after the method name.
            var parenIdx = FindParamListOpenParen(body);
            var beforeParen = body.Substring(0, parenIdx).Trim();
            var paramsStr = body.Substring(parenIdx + 1, body.LastIndexOf(')') - parenIdx - 1).Trim();

            // Extract generics: find the '<' that starts the generic parameter list
            string genericsRaw = null;
            var beforeGenerics = beforeParen;
            var angleIdx = FindGenericOpenAngle(beforeParen);
            if (angleIdx >= 0)
            {
                genericsRaw = beforeParen.Substring(angleIdx + 1, beforeParen.LastIndexOf('>') - angleIdx - 1).Trim();
                beforeGenerics = beforeParen.Substring(0, angleIdx).Trim();
            }

            // beforeGenerics is now "!!T Read" or "native int ByteOffset" etc.
            // Method name is the last word
            var lastSpace = beforeGenerics.LastIndexOf(' ');
            var methodName = beforeGenerics.Substring(lastSpace + 1);
            var ilReturnType = beforeGenerics.Substring(0, lastSpace).Trim();

            // Parse generics
            var genericParams = ParseGenericParams(genericsRaw);

            // Parse parameters
            var parameters = ParseParameters(paramsStr);

            // Map return type
            var csReturnType = MapReturnType(ilReturnType);

            var method = new MethodInfo
            {
                Name = methodName,
                CsReturnType = csReturnType,
                GenericParams = genericParams,
                Parameters = parameters,
            };

            return method;
        }

        /// <summary>
        /// Find the '(' that starts the parameter list, skipping any '(' inside generic constraints
        /// like &lt;valuetype .ctor ([CORE_ASSEMBLY]System.ValueType) Tglt;
        /// </summary>
        private static int FindParamListOpenParen(string body)
        {
            var depth = 0;
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] == '<') depth++;
                else if (body[i] == '>') depth--;
                else if (body[i] == '(' && depth == 0)
                    return i;
            }
            throw new InvalidOperationException($"Could not find parameter list in: {body}");
        }

        /// <summary>
        /// Find the '&lt;' that starts the generic parameter list on the method name.
        /// This is the '&lt;' that comes after the method name, not one in the return type.
        /// We scan backwards from the parameter list paren.
        /// </summary>
        private static int FindGenericOpenAngle(string beforeParen)
        {
            // If there's no '<' at all, no generics
            if (!beforeParen.Contains('<'))
                return -1;

            // The generic '<' comes after the method name (the last word before '<').
            // Find the last '>' and then find its matching '<'
            var lastClose = beforeParen.LastIndexOf('>');
            if (lastClose < 0)
                return -1;

            var depth = 0;
            for (var i = lastClose; i >= 0; i--)
            {
                if (beforeParen[i] == '>') depth++;
                else if (beforeParen[i] == '<') depth--;
                if (depth == 0)
                    return i;
            }

            return -1;
        }

        private static List<GenericParam> ParseGenericParams(string genericsRaw)
        {
            var result = new List<GenericParam>();
            if (string.IsNullOrWhiteSpace(genericsRaw))
                return result;

            // Split by ',' but respect nested parens/angles
            var parts = SplitRespectingNesting(genericsRaw, ',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("class "))
                {
                    // class constraint: "class T"
                    result.Add(new GenericParam
                    {
                        Name = trimmed.Substring("class ".Length).Trim(),
                        Constraint = "class"
                    });
                }
                else if (trimmed.StartsWith("valuetype"))
                {
                    // struct constraint: "valuetype .ctor ([CORE_ASSEMBLY]System.ValueType) T"
                    // Name is the last word
                    var name = trimmed.Split(' ').Last();
                    result.Add(new GenericParam
                    {
                        Name = name,
                        Constraint = "struct"
                    });
                }
                else
                {
                    // No constraint
                    result.Add(new GenericParam { Name = trimmed });
                }
            }

            return result;
        }

        private static List<ParamInfo> ParseParameters(string paramsStr)
        {
            var result = new List<ParamInfo>();
            if (string.IsNullOrWhiteSpace(paramsStr))
                return result;

            var parts = SplitRespectingNesting(paramsStr, ',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                // Check for [out] prefix
                var isOut = false;
                if (trimmed.StartsWith("[out]"))
                {
                    isOut = true;
                    trimmed = trimmed.Substring("[out]".Length).Trim();
                }

                // The parameter is "TYPE 'name'" or "TYPE name"
                // Split at the last space to get type and name
                var lastSpace = trimmed.LastIndexOf(' ');
                var ilType = trimmed.Substring(0, lastSpace).Trim();
                var name = trimmed.Substring(lastSpace + 1).Trim().Trim('\'');

                var csType = MapParamType(ilType, isOut);

                result.Add(new ParamInfo
                {
                    CsType = csType,
                    Name = name,
                });
            }

            return result;
        }

        private static string MapParamType(string ilType, bool isOut)
        {
            // Check for generic type param references: !!Name or !!Name&
            if (ilType.StartsWith("!!"))
            {
                var name = ilType.Substring(2);
                if (name.EndsWith("&"))
                {
                    name = name.TrimEnd('&');
                    return isOut ? $"out {name}" : $"ref {name}";
                }
                return name;
            }

            if (TypeMap.TryGetValue(ilType, out var mapped))
                return mapped;

            throw new InvalidOperationException($"Unknown IL parameter type: {ilType}");
        }

        private static string MapReturnType(string ilReturnType)
        {
            // Handle ref returns: "!!T&", "!!TTo&"
            if (ilReturnType.StartsWith("!!") && ilReturnType.EndsWith("&"))
            {
                var name = ilReturnType.Substring(2).TrimEnd('&');
                return $"ref {name}";
            }

            // Handle value returns: "!!T"
            if (ilReturnType.StartsWith("!!"))
            {
                return ilReturnType.Substring(2);
            }

            if (TypeMap.TryGetValue(ilReturnType, out var mapped))
                return mapped;

            throw new InvalidOperationException($"Unknown IL return type: {ilReturnType}");
        }

        private static List<string> SplitRespectingNesting(string input, char separator)
        {
            var result = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (input[i] == '(' || input[i] == '<' || input[i] == '[') depth++;
                else if (input[i] == ')' || input[i] == '>' || input[i] == ']') depth--;
                else if (input[i] == separator && depth == 0)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(input.Substring(start));
            return result;
        }

        private static string EmitCSharpFile(List<MethodInfo> methods)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Runtime.Versioning;");
            sb.AppendLine("using InlineIL;");
            sb.AppendLine("using static InlineIL.IL.Emit;");
            sb.AppendLine();
            sb.AppendLine("// System.Runtime.CompilerServices.Unsafe does not have nullable reference type annotations");
            sb.AppendLine("#nullable disable");
            sb.AppendLine();
            sb.AppendLine("namespace System.Runtime.CompilerServices.Unsafe");
            sb.AppendLine("{");
            sb.AppendLine("    [SuppressMessage(\"ReSharper\", \"UnusedType.Global\")]");
            sb.AppendLine("    [SuppressMessage(\"ReSharper\", \"UnusedMember.Global\")]");
            sb.AppendLine("    [SuppressMessage(\"ReSharper\", \"UnusedParameter.Global\")]");
            sb.AppendLine("    [SuppressMessage(\"ReSharper\", \"EntityNameCapturedOnly.Global\")]");
            sb.AppendLine("    public static unsafe class Unsafe");
            sb.AppendLine("    {");

            for (var i = 0; i < methods.Count; i++)
            {
                if (i > 0) sb.AppendLine();
                EmitMethod(sb, methods[i]);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void EmitMethod(StringBuilder sb, MethodInfo method)
        {
            // Attributes
            sb.AppendLine($"{Indent}[NonVersionable]");
            sb.AppendLine($"{Indent}[MethodImpl(MethodImplOptions.AggressiveInlining)]");

            // Signature
            var genericsStr = method.GenericParams.Count > 0
                ? "<" + string.Join(", ", method.GenericParams.Select(g => g.Name)) + ">"
                : "";

            var paramsStr = string.Join(", ", method.Parameters.Select(p => $"{p.CsType} {p.Name}"));

            sb.Append($"{Indent}public static {method.CsReturnType} {method.Name}{genericsStr}({paramsStr})");

            // Generic constraints
            var constraints = method.GenericParams
                .Where(g => g.Constraint != null)
                .Select(g => $"where {g.Name} : {g.Constraint}")
                .ToList();

            if (constraints.Count > 0)
            {
                sb.AppendLine();
                foreach (var constraint in constraints)
                {
                    sb.Append($"{Indent}    {constraint}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"{Indent}{{");

            // Method body — check for special cases
            if (IsAsRefWithLocals(method))
            {
                EmitAsRefWithLocalsBody(sb, method);
            }
            else if (IsSkipInit(method))
            {
                EmitSkipInitBody(sb);
            }
            else
            {
                EmitStandardBody(sb, method);
            }

            sb.AppendLine($"{Indent}}}");
        }

        private static void EmitStandardBody(StringBuilder sb, MethodInfo method)
        {
            foreach (var il in method.IlInstructions)
            {
                var csLine = ConvertInstruction(il, method);
                sb.AppendLine($"{Indent}    {csLine} // {il}");
            }

            // Return statement
            var ret = GetReturnStatement(method);
            if (ret != null)
            {
                sb.AppendLine($"{Indent}    {ret} // ret");
            }
        }

        private static bool IsAsRefWithLocals(MethodInfo method)
        {
            return method.HasLocals && method.Name == "AsRef";
        }

        private static bool IsSkipInit(MethodInfo method)
        {
            return method.MaxStack == 0;
        }

        private static void EmitAsRefWithLocalsBody(StringBuilder sb, MethodInfo method)
        {
            // Special case: AsRef<T>(void* source)
            // For .NET Core the roundtrip via a local is no longer needed
            var sourceParam = method.Parameters[0].Name;

            sb.AppendLine($"{Indent}    // For .NET Core the roundtrip via a local is no longer needed");
            sb.AppendLine("#if NETCOREAPP");
            sb.AppendLine($"{Indent}    IL.Push({sourceParam}); // ldarg.0");
            sb.AppendLine($"{Indent}    return ref IL.ReturnRef<{method.GenericParams[0].Name}>(); // ret");
            sb.AppendLine("#else");
            sb.AppendLine($"{Indent}    // Roundtrip via a local to avoid type mismatch on return that the JIT inliner chokes on.");
            sb.AppendLine($"{Indent}    IL.DeclareLocals(");
            sb.AppendLine($"{Indent}        false,");
            sb.AppendLine($"{Indent}        new LocalVar(\"local\", typeof(int).MakeByRefType())");
            sb.AppendLine($"{Indent}    );");
            sb.AppendLine();
            sb.AppendLine($"{Indent}    IL.Push({sourceParam}); // ldarg.0");
            sb.AppendLine($"{Indent}    Stloc(\"local\"); // stloc.0");
            sb.AppendLine($"{Indent}    Ldloc(\"local\"); // ldloc.0");
            sb.AppendLine($"{Indent}    return ref IL.ReturnRef<{method.GenericParams[0].Name}>(); // ret");
            sb.AppendLine("#endif");
        }

        private static void EmitSkipInitBody(StringBuilder sb)
        {
            // Special case: SkipInit<T>(out T value) — maxstack 0, just ret
            sb.AppendLine($"{Indent}    Ret(); // ret");
            sb.AppendLine($"{Indent}    throw IL.Unreachable();");
        }

        private static string ConvertInstruction(string il, MethodInfo method)
        {
            // ldarg.N → Ldarg(nameof(paramN))
            if (il.StartsWith("ldarg."))
            {
                var idx = int.Parse(il.Substring("ldarg.".Length));
                return $"Ldarg(nameof({method.Parameters[idx].Name}));";
            }

            // ldobj !!T → Ldobj(typeof(T))
            if (il.StartsWith("ldobj "))
            {
                var typeName = il.Substring("ldobj ".Length).TrimStart('!', '!');
                return $"Ldobj(typeof({typeName}));";
            }

            // stobj !!T → Stobj(typeof(T))
            if (il.StartsWith("stobj "))
            {
                var typeName = il.Substring("stobj ".Length).TrimStart('!', '!');
                return $"Stobj(typeof({typeName}));";
            }

            // sizeof !!T → Sizeof(typeof(T))
            if (il.StartsWith("sizeof "))
            {
                var typeName = il.Substring("sizeof ".Length).TrimStart('!', '!');
                return $"Sizeof(typeof({typeName}));";
            }

            // unbox !!T → IL.Emit.Unbox(typeof(T))  (FQ to avoid conflict with method name)
            if (il.StartsWith("unbox "))
            {
                var typeName = il.Substring("unbox ".Length).TrimStart('!', '!');
                return $"IL.Emit.Unbox(typeof({typeName}));";
            }

            // unaligned. 0x1 or 0x01 → Unaligned(1)
            if (il.StartsWith("unaligned."))
            {
                return "Unaligned(1);";
            }

            // Simple instruction mappings
            return il switch
            {
                "cpblk" => "Cpblk();",
                "initblk" => "Initblk();",
                "conv.u" => "Conv_U();",
                "conv.i" => "Conv_I();",
                "mul" => "Mul();",
                "add" => "IL.Emit.Add();",   // FQ to avoid conflict with method name Add<T>
                "sub" => "Sub();",
                "ceq" => "Ceq();",
                "cgt.un" => "Cgt_Un();",
                "clt.un" => "Clt_Un();",
                "ldc.i4.0" => "Ldc_I4_0();",
                "stloc.0" => "Stloc(\"local\");",
                "ldloc.0" => "Ldloc(\"local\");",
                _ => throw new InvalidOperationException($"Unknown IL instruction: {il}")
            };
        }

        private static string GetReturnStatement(MethodInfo method)
        {
            var csReturn = method.CsReturnType;

            if (csReturn == "void")
                return null;

            if (csReturn == "void*")
                return "return IL.ReturnPointer();";

            if (csReturn == "int")
                return "return IL.Return<int>();";

            if (csReturn == "IntPtr")
                return "return IL.Return<IntPtr>();";

            if (csReturn == "bool")
                return "return IL.Return<bool>();";

            if (csReturn.StartsWith("ref "))
            {
                var typeName = csReturn.Substring("ref ".Length);
                return $"return ref IL.ReturnRef<{typeName}>();";
            }

            // Generic type param return (e.g., T)
            if (method.GenericParams.Any(g => g.Name == csReturn))
            {
                return $"return IL.Return<{csReturn}>();";
            }

            throw new InvalidOperationException($"Cannot determine return statement for type: {csReturn}");
        }

        private enum ParserState
        {
            Preamble,
            WaitingForClassOpen,
            ClassBody,
            MethodSignature,
            WaitingForMethodOpen,
            MethodBody,
            Done,
        }

        private class MethodInfo
        {
            public string Name { get; set; }
            public string CsReturnType { get; set; }
            public List<GenericParam> GenericParams { get; set; } = new();
            public List<ParamInfo> Parameters { get; set; } = new();
            public List<string> IlInstructions { get; set; } = new();
            public int MaxStack { get; set; } = -1;
            public bool HasLocals { get; set; }
        }

        private class GenericParam
        {
            public string Name { get; set; }
            public string Constraint { get; set; } // "class", "struct", or null
        }

        private class ParamInfo
        {
            public string CsType { get; set; }
            public string Name { get; set; }
        }
    }
}
