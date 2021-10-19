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

        if (toolPathProperty is null)
        {
            return config;
        }

        string currentTool = toolPathProperty.GetValue(config)?.ToString();

        if (!string.IsNullOrWhiteSpace(prefixTool))
        {
            prefixTool = prefixTool.Replace("{dotnetTool}", currentTool);
            string tool = $"{prefixTool} {currentTool}";
            toolPathProperty.SetValue(config, tool);
            Logger.Info($"Final DotNetTool = {tool}");

        }
        return config;
    }
}
