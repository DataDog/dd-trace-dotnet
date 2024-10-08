using Nuke.Common;
using Nuke.Common.Execution;

public partial class BuildFinishedNotificationAttribute : BuildExtensionAttributeBase, IOnBuildFinished
{
#if !NUKE_NOTIFY
    public void OnBuildFinished()
    {}
#endif
}

