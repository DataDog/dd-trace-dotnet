using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nuke.Common.ProjectModel;
using Logger = Serilog.Log;

public static class ProjectExtensions
{
    static readonly ConcurrentDictionary<string, Microsoft.Build.Evaluation.Project> MsBuildProjects = new();
    public static IReadOnlyCollection<string> TryGetTargetFrameworks(this Project project)
    {
        try
        {
            // Using GetMsBuildProject() instead of built-in so that we can cache the MSBuild projects,
            // because this is very expensive
            return GetMsBuildProject(project)?.GetSplittedPropertyValue("TargetFramework", "TargetFrameworks");
        }
        catch (Exception ex)
        {
            Logger.Information($"Error fetching target frameworks for {project?.Name}: {ex}");
            return null;
        }
    }

    public static DockerDependencyType RequiresDockerDependency(this Project project)
    {
        try
        {
            // Using GetMsBuildProject() instead of built-in so that we can cache the MSBuild projects,
            // because this is very expensive
            var propertyValue = GetMsBuildProject(project).GetProperty("RequiresDockerDependency")?.EvaluatedValue;
            if (propertyValue is null)
            {
                return DockerDependencyType.None;
            }

            return Enum.Parse<DockerDependencyType>(propertyValue);
        }
        catch (Exception ex)
        {
            Logger.Information($"Error checking RequiresDockerDependency for {project?.Name}: {ex}");
            return DockerDependencyType.None;
        }
    }

    static Microsoft.Build.Evaluation.Project GetMsBuildProject(Project project)
    {
        return MsBuildProjects.GetOrAdd(project.Path, x => ProjectModelTasks.ParseProject(x));
    }

    // Based on Nuke.Common.ProjectModel.GetSplittedPropertyValue
    static IReadOnlyCollection<string> GetSplittedPropertyValue(
        this Microsoft.Build.Evaluation.Project msbuildProject,
        params string[] names)
    {
        foreach (var name in names)
        {
            var property = msbuildProject.GetProperty(name);
            if (property != null)
                return property.EvaluatedValue.Split(';');
        }

        return null;
    }
}
