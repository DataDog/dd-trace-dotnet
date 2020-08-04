namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Span tags for build data model
    /// </summary>
    internal static class BuildTags
    {
        /// <summary>
        /// Build operation name
        /// </summary>
        public const string BuildOperationName = "msbuild.build";

        /// <summary>
        /// Build name
        /// </summary>
        public const string BuildName = "build.name";

        /// <summary>
        /// Build command
        /// </summary>
        public const string BuildCommand = "build.command";

        /// <summary>
        /// Build working folder
        /// </summary>
        public const string BuildWorkingFolder = "build.working_folder";

        /// <summary>
        /// Build environment
        /// </summary>
        public const string BuildEnvironment = "build.environment";

        /// <summary>
        /// Build start message
        /// </summary>
        public const string BuildStartMessage = "build.start_message";

        /// <summary>
        /// Build start message
        /// </summary>
        public const string BuildEndMessage = "build.end_message";

        /// <summary>
        /// Build status
        /// </summary>
        public const string BuildStatus = "build.status";

        /// <summary>
        /// Build succeeded status
        /// </summary>
        public const string BuildSucceededStatus = "SUCCEEDED";

        /// <summary>
        /// Build failed status
        /// </summary>
        public const string BuildFailedStatus = "FAILED";

        /// <summary>
        /// Project properties
        /// </summary>
        public const string ProjectProperties = "project.properties";

        /// <summary>
        /// Project filename
        /// </summary>
        public const string ProjectFile = "project.file";

        /// <summary>
        /// Project sender entity
        /// </summary>
        public const string ProjectSenderName = "project.sender_name";

        /// <summary>
        /// Project target names
        /// </summary>
        public const string ProjectTargetNames = "project.target_names";

        /// <summary>
        /// Project tools version
        /// </summary>
        public const string ProjectToolsVersion = "project.tools_version";

        /// <summary>
        /// Error message
        /// </summary>
        public const string ErrorMessage = "error.msg";

        /// <summary>
        /// Error type
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// Error code
        /// </summary>
        public const string ErrorCode = "error.code";

        /// <summary>
        /// Error file
        /// </summary>
        public const string ErrorFile = "error.file";

        /// <summary>
        /// Error start line
        /// </summary>
        public const string ErrorStartLine = "error.start_location.line";

        /// <summary>
        /// Error start column
        /// </summary>
        public const string ErrorStartColumn = "error.start_location.column";

        /// <summary>
        /// Error end line
        /// </summary>
        public const string ErrorEndLine = "error.end_location.line";

        /// <summary>
        /// Error end column
        /// </summary>
        public const string ErrorEndColumn = "error.end_location.column";

        /// <summary>
        /// Error project file
        /// </summary>
        public const string ErrorProjectFile = "error.project_file";

        /// <summary>
        /// Error sub category
        /// </summary>
        public const string ErrorSubCategory = "error.sub_category";

        /// <summary>
        /// Error stack
        /// </summary>
        public const string ErrorStack = "error.stack";
    }
}
