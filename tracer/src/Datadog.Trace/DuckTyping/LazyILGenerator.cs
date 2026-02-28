// <copyright file="LazyILGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    // ReSharper disable once InconsistentNaming

    /// <summary>
    /// Represents lazy il generator.
    /// </summary>
    internal sealed class LazyILGenerator
    {
        /// <summary>
        /// Stores generator.
        /// </summary>
        private readonly ILGenerator? _generator;

        /// <summary>
        /// Stores instructions.
        /// </summary>
        private readonly List<Action<ILGenerator>> _instructions;
        private int _offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyILGenerator"/> class.
        /// </summary>
        /// <param name="generator">The generator value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public LazyILGenerator(ILGenerator? generator)
        {
            _generator = generator;
            _instructions = new List<Action<ILGenerator>>(16);
        }

        public int Offset => _offset;

        public int Count => _instructions.Count;

        /// <summary>
        /// Sets set offset.
        /// </summary>
        /// <param name="value">The value value.</param>
        public void SetOffset(int value)
        {
            if (value > _instructions.Count)
            {
                _offset = _instructions.Count;
            }
            else
            {
                _offset = value;
            }
        }

        /// <summary>
        /// Resets reset offset.
        /// </summary>
        public void ResetOffset()
        {
            _offset = _instructions.Count;
        }

        /// <summary>
        /// Executes begin scope.
        /// </summary>
        public void BeginScope()
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.BeginScope());
            }
            else
            {
                _instructions.Insert(_offset, il => il.BeginScope());
            }

            _offset++;
        }

        /// <summary>
        /// Executes declare local.
        /// </summary>
        /// <param name="localType">The local type value.</param>
        /// <param name="pinned">The pinned value.</param>
        /// <returns>The result produced by this operation.</returns>
        public LocalBuilder? DeclareLocal(Type localType, bool pinned)
        {
            return _generator?.DeclareLocal(localType, pinned);
        }

        /// <summary>
        /// Executes declare local.
        /// </summary>
        /// <param name="localType">The local type value.</param>
        /// <returns>The result produced by this operation.</returns>
        public LocalBuilder? DeclareLocal(Type localType)
        {
            return _generator?.DeclareLocal(localType);
        }

        /// <summary>
        /// Executes define label.
        /// </summary>
        /// <returns>The result produced by this operation.</returns>
        public Label DefineLabel()
        {
            return _generator?.DefineLabel() ?? default;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="str">The str value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, string str)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, str));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, str));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="field">The field value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, FieldInfo field)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, field));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, field));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="labels">The labels value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, Label[] labels)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, labels));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, labels));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="label">The label value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, Label label)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, label));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, label));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="local">The local value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, LocalBuilder local)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, local));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, local));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, float arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, byte arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, sbyte arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, short arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, double arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="meth">The meth value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, MethodInfo meth)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, meth));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, meth));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, int arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="arg">The arg value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, long arg)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, arg));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, arg));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="cls">The cls value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, Type cls)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, cls));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, cls));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="signature">The signature value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, SignatureHelper signature)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, signature));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, signature));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="con">The con value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void Emit(OpCode opcode, ConstructorInfo con)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.Emit(opcode, con));
            }
            else
            {
                _instructions.Insert(_offset, il => il.Emit(opcode, con));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit call.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="methodInfo">The method info value.</param>
        /// <param name="optionalParameterTypes">The optional parameter types value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EmitCall(opcode, methodInfo, optionalParameterTypes));
            }
            else
            {
                _instructions.Insert(_offset, il => il.EmitCall(opcode, methodInfo, optionalParameterTypes));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit calli.
        /// </summary>
        /// <param name="opcode">The opcode value.</param>
        /// <param name="callingConvention">The calling convention value.</param>
        /// <param name="returnType">The return type value.</param>
        /// <param name="parameterTypes">The parameter types value.</param>
        /// <param name="optionalParameterTypes">The optional parameter types value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes));
            }
            else
            {
                _instructions.Insert(_offset, il => il.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit write line.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void EmitWriteLine(string value)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EmitWriteLine(value));
            }
            else
            {
                _instructions.Insert(_offset, il => il.EmitWriteLine(value));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit write line.
        /// </summary>
        /// <param name="fld">The fld value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void EmitWriteLine(FieldInfo fld)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EmitWriteLine(fld));
            }
            else
            {
                _instructions.Insert(_offset, il => il.EmitWriteLine(fld));
            }

            _offset++;
        }

        /// <summary>
        /// Emits emit write line.
        /// </summary>
        /// <param name="localBuilder">The local builder value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public void EmitWriteLine(LocalBuilder localBuilder)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EmitWriteLine(localBuilder));
            }
            else
            {
                _instructions.Insert(_offset, il => il.EmitWriteLine(localBuilder));
            }

            _offset++;
        }

        /// <summary>
        /// Executes end scope.
        /// </summary>
        public void EndScope()
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.EndScope());
            }
            else
            {
                _instructions.Insert(_offset, il => il.EndScope());
            }

            _offset++;
        }

        /// <summary>
        /// Executes mark label.
        /// </summary>
        /// <param name="loc">The loc value.</param>
        public void MarkLabel(Label loc)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.MarkLabel(loc));
            }
            else
            {
                _instructions.Insert(_offset, il => il.MarkLabel(loc));
            }

            _offset++;
        }

        /// <summary>
        /// Throws the exception associated with throw exception.
        /// </summary>
        /// <param name="excType">The exc type value.</param>
        public void ThrowException(Type excType)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.ThrowException(excType));
            }
            else
            {
                _instructions.Insert(_offset, il => il.ThrowException(excType));
            }

            _offset++;
        }

        /// <summary>
        /// Executes using namespace.
        /// </summary>
        /// <param name="usingNamespace">The using namespace value.</param>
        public void UsingNamespace(string usingNamespace)
        {
            if (_offset == _instructions.Count)
            {
                _instructions.Add(il => il.UsingNamespace(usingNamespace));
            }
            else
            {
                _instructions.Insert(_offset, il => il.UsingNamespace(usingNamespace));
            }

            _offset++;
        }

        /// <summary>
        /// Executes flush.
        /// </summary>
        public void Flush()
        {
            if (_generator is not null)
            {
                foreach (Action<ILGenerator> instruction in _instructions)
                {
                    instruction(_generator);
                }
            }

            _instructions.Clear();
            _offset = 0;
        }
    }
}
