// <copyright file="CodeOwnersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class CodeOwnersTests
    {
        private readonly CodeOwners _githubCodeOwners;
        private readonly CodeOwners _gitlabCodeOwners;

        public CodeOwnersTests()
        {
            var ciDataFolder = DataHelpers.GetCiDataDirectory();

            var githubCodeOwnersFile = Path.Combine(ciDataFolder, "CODEOWNERS_GITHUB");
            _githubCodeOwners = new CodeOwners(githubCodeOwnersFile, CodeOwners.Platform.GitHub);

            var gitlabCodeOwnersFile = Path.Combine(ciDataFolder, "CODEOWNERS_GITLAB");
            _gitlabCodeOwners = new CodeOwners(gitlabCodeOwnersFile, CodeOwners.Platform.GitLab);
        }

        [SkippableTheory]
        // Existing baseline expectations
        [InlineData("unexistent/path/test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
        [InlineData("apps/test.cs", "[\"@octocat\"]")]
        [InlineData("/example/apps/test.cs", "[\"@octocat\"]")]
        [InlineData("/docs/test.cs", "[\"@doctocat\"]")]
        [InlineData("/examples/docs/test.cs", "[\"docs@example.com\"]")]
        [InlineData("/src/vendor/match.go", "[\"docs@example.com\"]")]
        [InlineData("/examples/docs/inside/test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
        [InlineData("/component/path/test.js", "[\"@js-owner\"]")]
        [InlineData("/mytextbox.txt", "[\"@octo-org/octocats\"]")]
        [InlineData("/scripts/artifacts/value.js", "[\"@doctocat\",\"@octocat\"]")]
        [InlineData("/apps/octo/test.cs", "[\"@octocat\"]")]
        [InlineData("/apps/github", null)]
        // Windows path separators
        [InlineData(@"unexistent\path\test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
        [InlineData(@"apps\test.cs", "[\"@octocat\"]")]
        [InlineData(@"\docs\test.cs", "[\"@doctocat\"]")]
        [InlineData(@"docs\getting-started.md", "[\"docs@example.com\"]")]
        [InlineData(@"\scripts\artifacts\value.js", "[\"@doctocat\",\"@octocat\"]")]
        [InlineData(@"\apps\github", null)]
        [InlineData(@"\x\logs\error.txt", "[\"@octo-org/octocats\"]")]
        // New GitHub quirks
        [InlineData("/x/logs/error.txt", "[\"@octo-org/octocats\"]")] // **/logs pattern
        [InlineData("docs/getting-started.md", "[\"docs@example.com\"]")] // docs/* pattern
        public void CheckGithubCodeOwners(string value, string expected)
        {
            var match = _githubCodeOwners.Match(value);
            var actual = match.Any() ? "[\"" + string.Join("\",\"", match.OrderBy(o => o)) + "\"]" : null;
            Assert.Equal(expected, actual);
        }

        [SkippableTheory]
        // Existing baseline expectations
        [InlineData("apps/README.md", "[\"@code\",\"@database\",\"@docs\",\"@multiple\",\"@owners\"]")]
        [InlineData("model/db", "[\"@code\",\"@database\",\"@multiple\",\"@owners\"]")]
        [InlineData("/config/data.conf", "[\"@config-owner\"]")]
        [InlineData("/docs/root.md", "[\"@root-docs\"]")]
        [InlineData("/docs/sub/root.md", "[\"@all-docs\"]")]
        [InlineData("/src/README", "[\"@group\",\"@group/with-nested/subgroup\"]")]
        [InlineData("/src/lib/internal.h", "[\"@lib-owner\"]")]
        [InlineData("src/ee/docs", "[\"@code\",\"@docs\",\"@multiple\",\"@owners\"]")]
        // Windows path separators
        [InlineData(@"apps\README.md", "[\"@code\",\"@database\",\"@docs\",\"@multiple\",\"@owners\"]")]
        [InlineData(@"model\db", "[\"@code\",\"@database\",\"@multiple\",\"@owners\"]")]
        [InlineData(@"\config\data.conf", "[\"@config-owner\"]")]
        [InlineData(@"\docs\root.md", "[\"@root-docs\"]")]
        [InlineData(@"\docs\sub\root.md", "[\"@all-docs\"]")]
        [InlineData(@"\src\README", "[\"@group\",\"@group/with-nested/subgroup\"]")]
        [InlineData(@"\src\lib\internal.h", "[\"@lib-owner\"]")]
        [InlineData(@"src\ee\docs", "[\"@code\",\"@docs\",\"@multiple\",\"@owners\"]")]
        [InlineData(@"path with spaces\example.txt", "[\"@space-owner\"]")]
        [InlineData(@"src\app\sample.rb", "[\"@ruby-owner\"]")]
        // New GitLab quirks present in existing fixture
        [InlineData("#file_with_pound.rb", "[\"@owner-file-with-pound\"]")] // escaped # char
        [InlineData("path with spaces/example.txt", "[\"@space-owner\"]")] // escaped spaces in path
        [InlineData("src/app/sample.rb", "[\"@ruby-owner\"]")] // *.rb pattern
        [InlineData("random/file.xyz", "[\"@code\",\"@multiple\",\"@owners\"]")] // last * rule wins
        [InlineData("LICENSE", "[\"@legal\",\"janedoe@gitlab.com\"]")] // username + email
        public void CheckGitlabCodeOwners(string value, string expected)
        {
            var match = _gitlabCodeOwners.Match(value);
            var actual = match.Any() ? "[\"" + string.Join("\",\"", match.OrderBy(o => o)) + "\"]" : null;
            Assert.Equal(expected, actual);
        }
    }
}
