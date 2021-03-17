using System.ComponentModel;
using Nuke.Common.Tooling;

[TypeConverter(typeof(TypeConverter<TargetFramework>))]
public class TargetFramework : Enumeration
{
    public static TargetFramework NET45 = new TargetFramework { Value = "net45" };
    public static TargetFramework NET461 = new TargetFramework { Value = "net461" };
    public static TargetFramework NETSTANDARD2_0 = new TargetFramework { Value = "netstandard2.0" };
    public static TargetFramework NETCOREAPP3_1 = new TargetFramework { Value = "netcoreapp3.1" };

    public static implicit operator string(TargetFramework framework)
    {
        return framework.Value;
    }
}

