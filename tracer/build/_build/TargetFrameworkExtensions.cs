public static class TargetFrameworkExtensions
{
    /// <summary>
    /// Is <paramref name="instance"/> greater than <paramref name="target"/>.
    /// Only works for .NET Core TFMs
    /// </summary>
    public static bool IsGreaterThan(this TargetFramework instance, TargetFramework target)
        => ((string) instance, (string) target) switch
        {
            // We ignore these, because they don't really count
            ("net461" or "net462" or "netstandard2.0", _) => false,
            (_, "net461" or "net462" or "netstandard2.0") => false,
            // real checks
            ("netcoreapp3.0" or "netcoreapp3.1" or "net5.0" or "net6.0" or "net7.0" or "net8.0", "netcoreapp2.1") => true,
            ("netcoreapp3.1" or "net5.0" or "net6.0" or "net7.0" or "net8.0", "netcoreapp3.0") => true,
            ("net5.0" or "net6.0" or "net7.0" or "net8.0", "netcoreapp3.1") => true,
            ("net6.0" or "net7.0" or "net8.0", "net5.0") => true,
            ("net7.0" or "net8.0", "net6.0") => true,
            ("net8.0", "net7.0") => true,
            _ => false,
        };

    /// <summary>
    /// Is <paramref name="instance"/> greater than <paramref name="target"/>.
    /// Only works for .NET Core TFMs
    /// </summary>
    public static bool IsGreaterThanOrEqualTo(this TargetFramework instance, TargetFramework target)
        => ((string) instance, (string) target) switch
        {
            ("net461" or "net462" or "netstandard2.0", _) => false,
            (_, "net461" or "net462" or "netstandard2.0") => false,
            ({ } x, { } y) when x == y => true,
            _ => instance.IsGreaterThan(target),
        };
}
