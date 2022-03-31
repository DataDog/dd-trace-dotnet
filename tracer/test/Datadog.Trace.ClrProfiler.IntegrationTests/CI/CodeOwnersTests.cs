// <copyright file="CodeOwnersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
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
            _githubCodeOwners = new CodeOwners(githubCodeOwnersFile);

            var gitlabCodeOwnersFile = Path.Combine(ciDataFolder, "CODEOWNERS_GITLAB");
            _gitlabCodeOwners = new CodeOwners(gitlabCodeOwnersFile);
        }

        [SkippableTheory]
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
        public void CheckGithubCodeOwners(string value, string expected)
        {
            var match = _githubCodeOwners.Match(value);
            Assert.True(match.HasValue);
            Assert.Equal(expected, match.Value.GetOwnersString());
        }

        [SkippableTheory]
        [InlineData("apps/README.md", "[\"@docs\",\"@database\",\"@multiple\",\"@code\",\"@owners\"]")]
        [InlineData("model/db", "[\"@database\",\"@multiple\",\"@code\",\"@owners\"]")]
        [InlineData("/config/data.conf", "[\"@config-owner\"]")]
        [InlineData("/docs/root.md", "[\"@root-docs\"]")]
        [InlineData("/docs/sub/root.md", "[\"@all-docs\"]")]
        [InlineData("/src/README", "[\"@group\",\"@group/with-nested/subgroup\"]")]
        [InlineData("/src/lib/internal.h", "[\"@lib-owner\"]")]
        [InlineData("src/ee/docs", "[\"@docs\",\"@multiple\",\"@code\",\"@owners\"]")]
        public void CheckGitlabCodeOwners(string value, string expected)
        {
            var match = _gitlabCodeOwners.Match(value);
            Assert.True(match.HasValue);
            Assert.Equal(expected, match.Value.GetOwnersString());
        }
    }
}
