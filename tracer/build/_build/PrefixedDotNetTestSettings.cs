using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

public static class PrefixedDotNetTestTask
{
    public static DotNetTestSettings SetPrefixTool(this DotNetTestSettings config, string prefixTool)
    {
        var toolPathProperty = typeof(ToolSettings).GetProperty("ProcessToolPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);

        string currentPath = toolPathProperty.GetValue(config)?.ToString();
        if (!string.IsNullOrWhiteSpace(prefixTool))
        {
            string tool = $"{prefixTool} {currentPath}";
            toolPathProperty.SetValue(config, tool);
            Logger.Info($"Final DotNetTool = {tool}");

        }
        return config;
    }
}
