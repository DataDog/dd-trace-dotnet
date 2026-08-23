// <copyright file="CodeOwnersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CodeOwnersTests
{
    private readonly CodeOwners _githubCodeOwners;
    private readonly CodeOwners _gitlabCodeOwners;

    public CodeOwnersTests()
    {
        var ciDataFolder = Path.Combine(
            EnvironmentTools.GetSolutionDirectory(),
            "tracer",
            "test",
            "Datadog.Trace.ClrProfiler.IntegrationTests",
            "CI",
            "Data");

        _githubCodeOwners = new CodeOwners(Path.Combine(ciDataFolder, "CODEOWNERS_GITHUB"), CodeOwners.Platform.GitHub);
        _gitlabCodeOwners = new CodeOwners(Path.Combine(ciDataFolder, "CODEOWNERS_GITLAB"), CodeOwners.Platform.GitLab);
    }

    [SkippableTheory]
    // Existing baseline expectations
    [InlineData("unexistent/path/test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
    [InlineData("apps/test.cs", "[\"@octocat\"]")]
    [InlineData("/example/apps/test.cs", "[\"@octocat\"]")]
    [InlineData("/docs/test.cs", "[\"@doctocat\"]")]
    [InlineData("/examples/docs/test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
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
    [InlineData(@"\examples\docs\test.cs", "[\"@global-owner1\",\"@global-owner2\"]")]
    [InlineData(@"docs\getting-started.md", "[\"@doctocat\"]")] // docs/* vs /docs/ precedence
    [InlineData(@"\scripts\artifacts\value.js", "[\"@doctocat\",\"@octocat\"]")]
    [InlineData(@"\apps\github", null)]
    [InlineData(@"\x\logs\error.txt", "[\"@octo-org/octocats\"]")]
    // New GitHub quirks
    [InlineData("/x/logs/error.txt", "[\"@octo-org/octocats\"]")] // matches the `*.txt` rule at any depth
    // Rooted patterns match regardless of a leading slash, so the later `/docs/` rule
    // (last match wins) takes precedence over the earlier `docs/*` rule.
    [InlineData("docs/getting-started.md", "[\"@doctocat\"]")] // docs/* vs /docs/ precedence
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
