﻿using System;
using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.ProjectModel;

public static class ProjectExtensions
{
    public static IReadOnlyCollection<string> TryGetTargetFrameworks(this Project project)
    {
        try
        {
            return project?.GetTargetFrameworks();
        }
        catch (Exception ex)
        {
            Logger.Info($"Error fetching target frameworks for {project?.Name}: {ex}");
            return null;
        }
    }

    public static bool RequiresDockerDependency(this Project project)
    {
        try
        {
            return bool.TryParse(project?.GetProperty("RequiresDockerDependency"), out var hasDockerDependency)
                && hasDockerDependency;
        }
        catch (Exception ex)
        {
            Logger.Info($"Error checking RequiresDockerDependency for {project?.Name}: {ex}");
            return false;
        }
    }
}
