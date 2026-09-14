# Repo context — dd-trace-dotnet

Read only by the orchestrator (Step 0 of `SKILL.md`), not by individual reviewers. Repo-specific; not part of the shared core.

## Related skills in this repo

Existing skills live under `.claude/skills/` (`bump-libdatadog`, `review-pr`, `analyze-crash`, `analyze-error`, `azure-functions`, `analyze-azdo-build`). This review skill is under `.agents/skills/`; `.claude/skills/dd-apm-sdk-review` is a symlink to it. No name clash.

Cite the others as authoritative for their area. Do not invoke them, and they must not invoke this skill:

- `review-pr` — posts a generic GitHub review; it is not the product-lens gate and must not replace this skill
- `bump-libdatadog` — libdatadog version bumps
- `analyze-crash` / `analyze-error` / `analyze-azdo-build` — runtime/CI failure analysis
