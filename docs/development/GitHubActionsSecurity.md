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

## Signed commits

Commits reaching `master` must be signed, and `repository.datadog.yml` grants no exceptions. A workflow therefore must never create a commit with `git push` — git-created commits are unsigned and will block the PR on the `commit-signatures` merge gate.

Instead, push through the GitHub API, which signs the commit server-side. Two options:

- **Creating or updating a pull request** — use the local composite action:
  ```yaml
  - uses: ./.github/actions/create-signed-pull-request
    with:
      token: ${{ steps.octo-sts.outputs.token }}
      branch: bot/my-branch
      commit-message: "[My Bot] Update things"
      title: "[My Bot] Update things"
      base: master
  ```
  It commits the working tree, pushes the commit signed, and creates or updates the PR. It replaces `peter-evans/create-pull-request`, which pushes over git and so cannot sign.

- **Pushing to a branch without a pull request** — stage the changes with `git add`, then use [`DataDog/commit-headless`](https://github.com/DataDog/commit-headless) directly with `command: commit`. It builds the commit from the index, so no local `git commit` is needed. See `create_hotfix_branch.yml` and `generate_package_versions.yml`.

Any installation token works, including the default `GITHUB_TOKEN`, so a job that only needs to push a commit does not need dd-octo-sts — just `permissions: contents: write`. Use dd-octo-sts where the existing reason for it still applies, such as needing a PR to trigger other workflows.

Background: [Commit Headless (sign bot commits)](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/5580588264) and the [Commit Signing Enforcement FAQ](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/5105058311).

---

## Reviewer checklist

When reviewing a PR that touches `.github/workflows/` or `.github/actions/`:

- [ ] Every new or changed `uses:` for a third-party action is pinned to a 40-char commit SHA (not a tag, branch, or version number).
- [ ] Every new action is on the allowlist (or the allowlist has been updated in the same/accompanying change).
- [ ] Local `./` refs (`uses: ./.github/actions/...`, `uses: ./.github/workflows/...`) are **not** version-pinned — leave them as-is.
- [ ] The `# vX.Y.Z` comment reflects the actual version the SHA resolves to.
- [ ] No step creates a commit with `git push` — see [Signed commits](#signed-commits).

