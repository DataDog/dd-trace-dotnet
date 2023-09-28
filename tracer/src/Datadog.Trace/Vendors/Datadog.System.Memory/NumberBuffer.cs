// Decompiled with JetBrains decompiler
// Type: System.NumberBuffer
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System.Diagnostics;
using System.Text;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System
{
    internal ref struct NumberBuffer
  {
    public int Scale;
    public bool IsNegative;
    public const int BufferSize = 51;
    private byte _b0;
    private byte _b1;
    private byte _b2;
    private byte _b3;
    private byte _b4;
    private byte _b5;
    private byte _b6;
    private byte _b7;
    private byte _b8;
    private byte _b9;
    private byte _b10;
    private byte _b11;
    private byte _b12;
    private byte _b13;
    private byte _b14;
    private byte _b15;
    private byte _b16;
    private byte _b17;
    private byte _b18;
    private byte _b19;
    private byte _b20;
    private byte _b21;
    private byte _b22;
    private byte _b23;
    private byte _b24;
    private byte _b25;
    private byte _b26;
    private byte _b27;
    private byte _b28;
    private byte _b29;
    private byte _b30;
    private byte _b31;
    private byte _b32;
    private byte _b33;
    private byte _b34;
    private byte _b35;
    private byte _b36;
    private byte _b37;
    private byte _b38;
    private byte _b39;
    private byte _b40;
    private byte _b41;
    private byte _b42;
    private byte _b43;
    private byte _b44;
    private byte _b45;
    private byte _b46;
    private byte _b47;
    private byte _b48;
    private byte _b49;
    private byte _b50;

    public unsafe Span<byte> Digits => new Span<byte>(Unsafe.AsPointer<byte>(ref this._b0), 51);

    public unsafe byte* UnsafeDigits => (byte*) Unsafe.AsPointer<byte>(ref this._b0);

    public int NumDigits => this.Digits.IndexOf<byte>((byte) 0);

    [Conditional("DEBUG")]
    public void CheckConsistency()
    {
    }

    public override string ToString()
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append('[');
      stringBuilder.Append('"');
      Span<byte> digits = this.Digits;
      for (int index = 0; index < 51; ++index)
      {
        byte num = digits[index];
        if (num != (byte) 0)
          stringBuilder.Append((char) num);
        else
          break;
      }
      stringBuilder.Append('"');
      stringBuilder.Append(", Scale = " + (object) this.Scale);
      stringBuilder.Append(", IsNegative   = " + this.IsNegative.ToString());
      stringBuilder.Append(']');
      return stringBuilder.ToString();
    }
  }
}
