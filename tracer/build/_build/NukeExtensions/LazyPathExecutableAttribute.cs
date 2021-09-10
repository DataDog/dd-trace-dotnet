using System;
using System.Reflection;
using Nuke.Common.Tooling;
using Nuke.Common.ValueInjection;

/// <summary>
///     Injects a delegate for process execution. The executable name is derived from the member name or can be
///     passed as constructor argument. The path to the executable is resolved in the following order:
///     <ul>
///         <li>From environment variables (e.g., <c>[NAME]_EXE=path</c>)</li>
///         <li>From the PATH variable using <c>which</c> or <c>where</c></li>
///     </ul>
/// </summary>
/// <example>
///     <code>
/// [PathExecutable] readonly Tool Echo;
/// Target FooBar => _ => _
///     .Executes(() =>
///     {
///         var output = Echo.Value("test");
///     });
///     </code>
/// </example>
public class LazyPathExecutableAttribute : ValueInjectionAttributeBase
{
    private readonly string _name;

    public LazyPathExecutableAttribute(string name = null)
    {
        _name = name;
    }

    public override object GetValue(MemberInfo member, object instance)
    {
        var name = _name ?? member.Name;
        return new Lazy<Tool>(() => ToolResolver.TryGetEnvironmentTool(name) ??
                                    ToolResolver.GetPathTool(name));
    }
}
