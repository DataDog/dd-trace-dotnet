using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Tooling;

[TypeConverter(typeof(TargetFrameworkTypeConverter))]
public class TargetFramework : Enumeration
{
    public static TargetFramework NET461 = new TargetFramework { Value = "net461" };
    public static TargetFramework NETSTANDARD2_0 = new TargetFramework { Value = "netstandard2.0" };
    public static TargetFramework NETCOREAPP2_1 = new TargetFramework { Value = "netcoreapp2.1" };
    public static TargetFramework NETCOREAPP3_0 = new TargetFramework { Value = "netcoreapp3.0" };
    public static TargetFramework NETCOREAPP3_1 = new TargetFramework { Value = "netcoreapp3.1" };
    public static TargetFramework NET5_0 = new TargetFramework { Value = "net5.0" };
    public static TargetFramework NET6_0 = new TargetFramework { Value = "net6.0" };
    public static TargetFramework NET7_0 = new TargetFramework { Value = "net7.0" };

    public static implicit operator string(TargetFramework framework)
    {
        return framework.Value;
    }

    public static TargetFramework[] GetFrameworks(TargetFramework[] except = null)
    {
        return typeof(TargetFramework)
              .GetFields(ReflectionService.Static)
              .Select(x => x.GetValue(null))
              .Cast<TargetFramework>()
              .Except(except ?? Array.Empty<TargetFramework>())
              .ToArray();
    }

    public class TargetFrameworkTypeConverter : TypeConverter<TargetFramework>
    {
        private static readonly TargetFramework[] AllTargetFrameworks = TargetFramework.GetFrameworks();

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                var matchingFields = AllTargetFrameworks
                    .Where(x=> string.Equals(x.Value, stringValue, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                ControlFlow.Assert(matchingFields.Count == 1, "matchingFields.Count == 1");
                return matchingFields.Single();
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}

