using Nuke.Common;
using Nuke.Common.Execution;

public partial class BuildFinishedNotificationAttribute : BuildExtensionAttributeBase, IOnBuildFinished
{
#if !IS_WINDOWS
    public void OnBuildFinished(NukeBuild build)
    {}
#endif
}

