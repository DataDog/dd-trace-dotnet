# TODO — Azure Functions Skill Improvements

## High Priority (correctness)

- [x] Fix `2>&1` stderr handling in `Set-EnvVars.ps1` and `Test-EnvVars.ps1`
  - `az` CLI warnings (deprecation notices, etc.) get mixed into JSON output as `ErrorRecord` objects, causing `ConvertFrom-Json` to fail
  - Files: `.claude/skills/azure-functions/Set-EnvVars.ps1` (lines 100, 205), `Test-EnvVars.ps1` (lines 79, 113, 126)
  - Fix: suppress stderr with `2>$null` on the happy path, re-run with `2>&1` only on error to capture the message
- [ ] Update `net6.0` default to `net8.0`
  - .NET 6 is EOL (Nov 2024); test matrix uses .NET 8 Isolated apps
  - Files: `tracer/tools/Deploy-AzureFunction.ps1` (`$TargetFramework` default), SKILL.md examples, `scripts-reference.md` examples

## Medium Priority (maintenance / clarity)

- [x] Remove `Find-NuGetConfig.ps1`
  - Both skill reviews flagged it as vestigial; documented as "no longer needed" in `scripts-reference.md`
  - File: `.claude/skills/azure-functions/Find-NuGetConfig.ps1`
  - Also remove references from `scripts-reference.md`
- [x] Trim `scripts-reference.md` duplication with SKILL.md
  - Deploy-AzureFunction.ps1 params, Get-AzureFunctionLogs.ps1 usage, Build-AzureFunctionsNuget.ps1 usage documented in both
  - Keep SKILL.md as the concise "how to use", scripts-reference.md as the detailed reference
- [ ] Extract troubleshooting + Datadog API sections from SKILL.md to supplementary files
  - "Common Troubleshooting" (~70 lines) and "Query Datadog API" (~25 lines) add bulk
  - Move to `troubleshooting.md` and keep reference links in SKILL.md
- [ ] Fix potential quoting issue in `Set-EnvVars.ps1` splatting
  - Complex JSON values in `DD_TRACE_SAMPLING_RULES` could break when passed via `@settingsArgs` to `az --settings`
  - File: `.claude/skills/azure-functions/Set-EnvVars.ps1` (line ~204)

## Low Priority (polish)

- [ ] Tighten the skill description in SKILL.md frontmatter
  - Currently ~60 words with some overlap between sentences
- [ ] Add `context: fork` frontmatter to SKILL.md
  - Keeps main conversation clean when handling large log outputs
- [ ] Add mid-workflow error recovery guidance to SKILL.md
  - Brief note on what to do when deploy succeeds but trigger fails, or logs show wrong version
- [ ] Add note in `log-analysis-guide.md` to prefer Read/Grep tools over bash grep
  - Agent should use dedicated tools for downloaded log files rather than bash grep patterns
