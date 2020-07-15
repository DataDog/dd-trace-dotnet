using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Build
{
    internal static class BuildTags
    {
        /// <summary>
        /// Build operation name
        /// </summary>
        public const string BuildOperationName = "build";

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

        /// <summary>
        /// Warning message
        /// </summary>
        public const string WarningMessage = "warning.msg";

        /// <summary>
        /// Warning type
        /// </summary>
        public const string WarningType = "warning.type";

        /// <summary>
        /// Warning code
        /// </summary>
        public const string WarningCode = "warning.code";

        /// <summary>
        /// Warning file
        /// </summary>
        public const string WarningFile = "warning.file";

        /// <summary>
        /// Warning start line
        /// </summary>
        public const string WarningStartLine = "warning.start_location.line";

        /// <summary>
        /// Warning start column
        /// </summary>
        public const string WarningStartColumn = "warning.start_location.column";

        /// <summary>
        /// Warning end line
        /// </summary>
        public const string WarningEndLine = "warning.end_location.line";

        /// <summary>
        /// Warning end column
        /// </summary>
        public const string WarningEndColumn = "warning.end_location.column";

        /// <summary>
        /// Warning project file
        /// </summary>
        public const string WarningProjectFile = "warning.project_file";

        /// <summary>
        /// Warning sub category
        /// </summary>
        public const string WarningSubCategory = "warning.sub_category";

        /// <summary>
        /// Warning stack
        /// </summary>
        public const string WarningStack = "warning.stack";

        /// <summary>
        /// GIT Repository
        /// </summary>
        public const string GitRepository = "git.repository";

        /// <summary>
        /// GIT Commit hash
        /// </summary>
        public const string GitCommit = "git.commit";

        /// <summary>
        /// GIT Branch name
        /// </summary>
        public const string GitBranch = "git.branch";

        /// <summary>
        /// Build Source root
        /// </summary>
        public const string BuildSourceRoot = "build.source_root";

        /// <summary>
        /// Build InContainer flag
        /// </summary>
        public const string BuildInContainer = "build.incontainer";

        /// <summary>
        /// CI Provider
        /// </summary>
        public const string CIProvider = "ci.provider";

        /// <summary>
        /// CI Build id
        /// </summary>
        public const string CIBuildId = "ci.build_id";

        /// <summary>
        /// CI Build number
        /// </summary>
        public const string CIBuildNumber = "ci.build_number";

        /// <summary>
        /// CI Build url
        /// </summary>
        public const string CIBuildUrl = "ci.build_url";

        /// <summary>
        /// Runtime name
        /// </summary>
        public const string RuntimeName = "runtime.name";

        /// <summary>
        /// Runtime os architecture
        /// </summary>
        public const string RuntimeOSArchitecture = "runtime.os_architecture";

        /// <summary>
        /// Runtime os platform
        /// </summary>
        public const string RuntimeOSPlatform = "runtime.os_platform";

        /// <summary>
        /// Runtime process architecture
        /// </summary>
        public const string RuntimeProcessArchitecture = "runtime.process_architecture";

        /// <summary>
        /// Runtime version
        /// </summary>
        public const string RuntimeVersion = "runtime.version";
    }
}
