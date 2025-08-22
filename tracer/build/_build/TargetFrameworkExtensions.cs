using System.Collections.Generic;
using System.Linq;

public static class TargetFrameworkExtensions
{
    private static readonly string[] IgnoredFrameworks = { "net461", "net48", "netstandard2.0" };

    private static readonly List<string> OrderedFrameworks = new()
    {
        "netcoreapp2.1",
        "netcoreapp3.0",
        "netcoreapp3.1",
        "net5.0",
        "net6.0",
        "net7.0",
        "net8.0",
        "net9.0",
        "net10.0"
    };

    /// <summary>
    /// Is <paramref name="instance"/> greater than <paramref name="target"/>.
    /// Only works for .NET Core TFMs
    /// </summary>
    public static bool IsGreaterThan(this TargetFramework instance, TargetFramework target)
    {
        var source = (string)instance;
        var compareTo = (string)target;

        if (IgnoredFrameworks.Contains(source) || IgnoredFrameworks.Contains(compareTo))
        {
            return false;
        }

        int sourceIndex = OrderedFrameworks.IndexOf(source);
        int targetIndex = OrderedFrameworks.IndexOf(compareTo);

        return sourceIndex > targetIndex;
    }

    /// <summary>
    /// Is <paramref name="instance"/> greater than <paramref name="target"/>.
    /// Only works for .NET Core TFMs
    /// </summary>
    public static bool IsGreaterThanOrEqualTo(this TargetFramework instance, TargetFramework target)
        => ((string) instance, (string) target) switch
        {
            ("net461" or "net48" or "netstandard2.0", _) => false,
            (_, "net461" or "net48" or "netstandard2.0") => false,
            ({ } x, { } y) when x == y => true,
            _ => instance.IsGreaterThan(target),
        };
}
