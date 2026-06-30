# GitHub Actions Security Policy

## Policy

All GitHub Actions workflow files (`.github/workflows/`) and composite actions (`.github/actions/`) must follow two rules:

1. **Every third-party action must be pinned to a full 40-character commit SHA**, with a trailing `# vX.Y.Z` comment for human readability.
2. **Only allowlisted actions may be used.** The allowlist is enforced via the GitHub repository/org settings ("Allow specified actions and reusable workflows").

### Why SHA-pinning?

A mutable tag (`@v4`, `@main`) can be force-pushed to point at different — potentially malicious — code at any time. A 40-char commit SHA is immutable: the action's code is locked to exactly that commit, regardless of what happens to tags or branches in the upstream repo.

### Exempt references

Local composite actions (`uses: ./.github/actions/...`) and local reusable workflows (`uses: ./.github/workflows/...`) move with the current commit and must **not** be version-pinned. Reviewers should leave these as-is. Also, all `actions/*`, `github/*`, and `DataDog/*` actions are allowed.

---

## Allowlist

The allowlist of permitted actions is managed by a repository admin in GitHub Settings → Actions → General → "Allow specified actions and reusable workflows" (or the equivalent IaC/Terraform field). It is not duplicated here to avoid going stale.

If a workflow run is blocked with an "action is not allowed" error, ask a repository admin to add the action to the allowlist before merging.

---

## Adding a new action

1. **Find the SHA** for the version you want. On the upstream repo, browse to the tag/release, note the full commit SHA (40 hex chars). Alternatively: `git ls-remote https://github.com/<owner>/<repo> refs/tags/<tag>` will print the commit SHA.
2. **Ask a repository admin to add the action to the allowlist** before the PR merges — otherwise the workflow will be blocked at runtime with an "action is not allowed" error.
3. **Write the `uses:` line** in the format:
   ```yaml
   uses: owner/repo@<40-char-sha> # vX.Y.Z
   ```

---

## SHA management (Dependabot)

SHA pins are kept current automatically. `.github/dependabot.yml` configures Dependabot for the `github-actions` ecosystem:
- Runs **monthly**, scanning `/.github/workflows/`, `/.github/actions/*`, and `/.github/actions/*/*`.
- Groups all updates into a single PR (`gh-actions-packages`).
- Applies a 2-day cooldown before raising the PR.

Dependabot preserves the SHA-pin + `# vX.Y.Z` comment format when bumping. Review the bump PR and spot-check that the new SHA corresponds to the advertised tag on the upstream repo before merging.

---

## Reviewer checklist

When reviewing a PR that touches `.github/workflows/` or `.github/actions/`:

- [ ] Every new or changed `uses:` for a third-party action is pinned to a 40-char commit SHA (not a tag, branch, or version number).
- [ ] Every new action is on the allowlist (or the allowlist has been updated in the same/accompanying change).
- [ ] Local `./` refs (`uses: ./.github/actions/...`, `uses: ./.github/workflows/...`) are **not** version-pinned — leave them as-is.
- [ ] The `# vX.Y.Z` comment reflects the actual version the SHA resolves to.

