﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ILOpCodeExtensions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata
{
  public static class ILOpCodeExtensions
  {
    /// <summary>
    /// Returns true of the specified op-code is a branch to a label.
    /// </summary>
    public static bool IsBranch(this ILOpCode opCode)
    {
      if ((uint) (opCode - (ushort) 43) > 25U)
      {
        switch (opCode)
        {
          case ILOpCode.Leave:
          case ILOpCode.Leave_s:
            break;
          default:
            return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Calculate the size of the specified branch instruction operand.
    /// </summary>
    /// <param name="opCode">Branch op-code.</param>
    /// <returns>1 if <paramref name="opCode" /> is a short branch or 4 if it is a long branch.</returns>
    /// <exception cref="T:System.ArgumentException">Specified <paramref name="opCode" /> is not a branch op-code.</exception>
    public static int GetBranchOperandSize(this ILOpCode opCode)
    {
      if ((uint) opCode <= 68U)
      {
        if ((uint) (opCode - (ushort) 43) > 12U)
        {
          if ((uint) (opCode - (ushort) 56) <= 12U)
            goto label_6;
          else
            goto label_7;
        }
      }
      else if (opCode != ILOpCode.Leave)
      {
        if (opCode != ILOpCode.Leave_s)
          goto label_7;
      }
      else
        goto label_6;
      return 1;
label_6:
      return 4;
label_7:
      throw new ArgumentException(SR.Format(SR.UnexpectedOpCode, (object) opCode), nameof (opCode));
    }

    /// <summary>Get a short form of the specified branch op-code.</summary>
    /// <param name="opCode">Branch op-code.</param>
    /// <returns>Short form of the branch op-code.</returns>
    /// <exception cref="T:System.ArgumentException">Specified <paramref name="opCode" /> is not a branch op-code.</exception>
    public static ILOpCode GetShortBranch(this ILOpCode opCode)
    {
      switch (opCode)
      {
        case ILOpCode.Br_s:
        case ILOpCode.Brfalse_s:
        case ILOpCode.Brtrue_s:
        case ILOpCode.Beq_s:
        case ILOpCode.Bge_s:
        case ILOpCode.Bgt_s:
        case ILOpCode.Ble_s:
        case ILOpCode.Blt_s:
        case ILOpCode.Bne_un_s:
        case ILOpCode.Bge_un_s:
        case ILOpCode.Bgt_un_s:
        case ILOpCode.Ble_un_s:
        case ILOpCode.Blt_un_s:
        case ILOpCode.Leave_s:
          return opCode;
        case ILOpCode.Br:
          return ILOpCode.Br_s;
        case ILOpCode.Brfalse:
          return ILOpCode.Brfalse_s;
        case ILOpCode.Brtrue:
          return ILOpCode.Brtrue_s;
        case ILOpCode.Beq:
          return ILOpCode.Beq_s;
        case ILOpCode.Bge:
          return ILOpCode.Bge_s;
        case ILOpCode.Bgt:
          return ILOpCode.Bgt_s;
        case ILOpCode.Ble:
          return ILOpCode.Ble_s;
        case ILOpCode.Blt:
          return ILOpCode.Blt_s;
        case ILOpCode.Bne_un:
          return ILOpCode.Bne_un_s;
        case ILOpCode.Bge_un:
          return ILOpCode.Bge_un_s;
        case ILOpCode.Bgt_un:
          return ILOpCode.Bgt_un_s;
        case ILOpCode.Ble_un:
          return ILOpCode.Ble_un_s;
        case ILOpCode.Blt_un:
          return ILOpCode.Blt_un_s;
        case ILOpCode.Leave:
          return ILOpCode.Leave_s;
        default:
          throw new ArgumentException(SR.Format(SR.UnexpectedOpCode, (object) opCode), nameof (opCode));
      }
    }

    /// <summary>Get a long form of the specified branch op-code.</summary>
    /// <param name="opCode">Branch op-code.</param>
    /// <returns>Long form of the branch op-code.</returns>
    /// <exception cref="T:System.ArgumentException">Specified <paramref name="opCode" /> is not a branch op-code.</exception>
    public static ILOpCode GetLongBranch(this ILOpCode opCode)
    {
      switch (opCode)
      {
        case ILOpCode.Br_s:
          return ILOpCode.Br;
        case ILOpCode.Brfalse_s:
          return ILOpCode.Brfalse;
        case ILOpCode.Brtrue_s:
          return ILOpCode.Brtrue;
        case ILOpCode.Beq_s:
          return ILOpCode.Beq;
        case ILOpCode.Bge_s:
          return ILOpCode.Bge;
        case ILOpCode.Bgt_s:
          return ILOpCode.Bgt;
        case ILOpCode.Ble_s:
          return ILOpCode.Ble;
        case ILOpCode.Blt_s:
          return ILOpCode.Blt;
        case ILOpCode.Bne_un_s:
          return ILOpCode.Bne_un;
        case ILOpCode.Bge_un_s:
          return ILOpCode.Bge_un;
        case ILOpCode.Bgt_un_s:
          return ILOpCode.Bgt_un;
        case ILOpCode.Ble_un_s:
          return ILOpCode.Ble_un;
        case ILOpCode.Blt_un_s:
          return ILOpCode.Blt_un;
        case ILOpCode.Br:
        case ILOpCode.Brfalse:
        case ILOpCode.Brtrue:
        case ILOpCode.Beq:
        case ILOpCode.Bge:
        case ILOpCode.Bgt:
        case ILOpCode.Ble:
        case ILOpCode.Blt:
        case ILOpCode.Bne_un:
        case ILOpCode.Bge_un:
        case ILOpCode.Bgt_un:
        case ILOpCode.Ble_un:
        case ILOpCode.Blt_un:
        case ILOpCode.Leave:
          return opCode;
        case ILOpCode.Leave_s:
          return ILOpCode.Leave;
        default:
          throw new ArgumentException(SR.Format(SR.UnexpectedOpCode, (object) opCode), nameof (opCode));
      }
    }
  }
}
