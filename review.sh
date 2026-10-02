#!/usr/bin/env bash

set -euo pipefail

ready=false
tool="${REVIEW_TOOL:-}"
base=""
review_log=""

usage() {
    cat <<'EOF'
Usage: ./review.sh [--ready] [--base <git-ref>] [--tool codex|claude]

Without --ready, review local changes without pushing anything. Committed
changes use the locally available default base; run git fetch first if needed.
For a stacked branch, specify its parent, for example:
  ./review.sh --base origin/feature-parent

With --ready, review the committed branch, push it, publish a GitHub status,
and mark its draft pull request as ready for review. The PR base is used
automatically, so --base cannot be combined with --ready.

Set REVIEW_TOOL=codex or REVIEW_TOOL=claude to choose a default reviewer.
EOF
}

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

cleanup() {
    if [[ -n "$review_log" ]]; then
        rm -f "$review_log"
    fi
}
trap cleanup EXIT

while [[ $# -gt 0 ]]; do
    case "$1" in
        --ready)
            ready=true
            ;;
        --tool)
            [[ $# -ge 2 ]] || fail "--tool requires codex or claude"
            tool="$2"
            shift
            ;;
        --base)
            [[ $# -ge 2 && "$2" != -* ]] || fail "--base requires a Git ref"
            base="$2"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "unknown argument: $1"
            ;;
    esac
    shift
done

[[ "$tool" == "" || "$tool" == "codex" || "$tool" == "claude" ]] ||
    fail "reviewer must be codex or claude"
[[ "$ready" != true || -z "$base" ]] ||
    fail "--base cannot be used with --ready; the PR base is selected automatically"

git rev-parse --is-inside-work-tree >/dev/null 2>&1 ||
    fail "run this script from inside a Git repository"
cd "$(git rev-parse --show-toplevel)"

choose_tool() {
    local has_codex=false
    local has_claude=false

    command -v codex >/dev/null 2>&1 && has_codex=true
    command -v claude >/dev/null 2>&1 && has_claude=true

    if [[ -n "$tool" ]]; then
        if [[ "$tool" == "codex" && "$has_codex" != true ]]; then
            fail "Codex CLI is not installed"
        fi
        if [[ "$tool" == "claude" && "$has_claude" != true ]]; then
            fail "Claude Code is not installed"
        fi
        return
    fi

    if [[ "$has_codex" == true && "$has_claude" == true ]]; then
        [[ -t 0 ]] || fail "both reviewers are installed; use --tool codex or --tool claude"
        printf 'Choose a reviewer:\n  1) Codex\n  2) Claude Code\n'
        if ! read -r -p "Selection [1-2]: " selection; then
            fail "could not read reviewer selection"
        fi
        case "$selection" in
            1) tool="codex" ;;
            2) tool="claude" ;;
            *) fail "invalid selection" ;;
        esac
    elif [[ "$has_codex" == true ]]; then
        tool="codex"
    elif [[ "$has_claude" == true ]]; then
        tool="claude"
    else
        fail "install Codex CLI or Claude Code before running a review"
    fi
}

default_base() {
    local ref

    if ref="$(git symbolic-ref --quiet --short refs/remotes/origin/HEAD 2>/dev/null)" &&
        git rev-parse --verify --quiet "${ref}^{commit}" >/dev/null; then
        printf '%s\n' "$ref"
        return
    fi

    for ref in origin/main origin/master main master; do
        if git rev-parse --verify --quiet "${ref}^{commit}" >/dev/null; then
            printf '%s\n' "$ref"
            return
        fi
    done

    return 1
}

reviewer_name() {
    if [[ "$tool" == "codex" ]]; then
        printf 'Codex'
    else
        printf 'Claude Code'
    fi
}

run_review_command() {
    local reviewer="$1"
    local report
    shift

    review_log="$(mktemp)" || fail "could not create a temporary file"
    if report="$("$@" 2>"$review_log")"; then
        if [[ -n "$report" ]]; then
            printf '%s\n' "$report"
            rm -f "$review_log"
            review_log=""
            return
        fi

        cat "$review_log" >&2
        fail "$reviewer completed without a review report"
    fi

    cat "$review_log" >&2
    if [[ -n "$report" ]]; then
        printf '%s\n' "$report"
    fi
    fail "$reviewer review failed"
}

run_codex_review() {
    run_review_command "Codex" codex review "$@"
}

run_claude_review() {
    run_review_command "Claude Code" \
        claude --permission-mode plan -p "$1"
}

print_numstat() {
    local added
    local deleted
    local path

    while IFS=$'\t' read -r added deleted path; do
        [[ -n "$path" ]] || continue
        if [[ "$added" == "-" || "$deleted" == "-" ]]; then
            printf '  %s (binary)\n' "$path"
        else
            printf '  %s (+%s -%s)\n' "$path" "$added" "$deleted"
        fi
    done
}

print_untracked() {
    local lines
    local path

    while IFS= read -r -d '' path; do
        if [[ ! -s "$path" ]]; then
            printf '  %s (+0 -0)\n' "$path"
        elif LC_ALL=C grep -Iq -- '' "./$path"; then
            lines="$(awk 'END { print NR }' "./$path")"
            printf '  %s (+%s -0)\n' "$path" "$lines"
        else
            printf '  %s (binary)\n' "$path"
        fi
    done < <(git ls-files --others --exclude-standard -z)
}

print_uncommitted_summary() {
    printf 'Uncommitted changes:\n'
    git diff --numstat HEAD | print_numstat
    print_untracked
}

review_uncommitted() {
    printf '\nRunning uncommitted review with %s...\n\n' "$(reviewer_name)"
    if [[ "$tool" == "codex" ]]; then
        run_codex_review --uncommitted
    else
        run_claude_review \
            "Review only staged, unstaged, and untracked changes relative to HEAD. Ignore committed changes. Do not modify files. Report only actionable findings with file and line references; if none, say so."
    fi
}

review_branch() {
    local base_ref="$1"
    local base_name="${2:-$base_ref}"

    printf '\nRunning review against %s with %s...\n\n' "$base_name" "$(reviewer_name)"
    if [[ "$tool" == "codex" ]]; then
        run_codex_review --base "$base_ref"
    else
        run_claude_review \
            "Review only committed changes in ${base_ref}...HEAD. Ignore staged, unstaged, and untracked changes. Do not modify files. Report only actionable findings with file and line references; if none, say so."
    fi
}

print_branch_summary() {
    local base_ref="$1"
    local base_name="${2:-$base_ref}"

    printf 'Committed changes against %s:\n' "$base_name"
    git diff --numstat "$base_ref"...HEAD | print_numstat
}

# Local review: no push and no GitHub changes.
if [[ "$ready" != true ]]; then
    working_tree_changed=true
    [[ -z "$(git status --porcelain)" ]] && working_tree_changed=false

    base_ref="$base"
    if [[ -z "$base_ref" ]]; then
        base_ref="$(default_base || true)"
    fi

    branch_changed=false
    if [[ -n "$base_ref" ]]; then
        git rev-parse --verify "${base_ref}^{commit}" >/dev/null 2>&1 ||
            fail "base branch $base_ref is not available locally"
        git diff --quiet "$base_ref"...HEAD || branch_changed=true
    elif [[ "$working_tree_changed" == true ]]; then
        printf 'No base branch found; reviewing uncommitted changes only. Use --base <git-ref> to include committed changes.\n'
    else
        fail "could not determine a base branch; use --base <git-ref>"
    fi

    if [[ "$branch_changed" != true && "$working_tree_changed" != true ]]; then
        printf 'No local changes to review.\n'
        exit 0
    fi

    choose_tool

    if [[ "$branch_changed" == true ]]; then
        review_branch "$base_ref"
    fi
    if [[ "$working_tree_changed" == true ]]; then
        review_uncommitted
    fi

    printf '\nReview completed with %s.\n' "$(reviewer_name)"
    if [[ "$branch_changed" == true ]]; then
        print_branch_summary "$base_ref"
    fi
    if [[ "$working_tree_changed" == true ]]; then
        print_uncommitted_summary
    fi
    exit 0
fi

# Ready-for-review handoff.
[[ -z "$(git status --porcelain)" ]] ||
    fail "--ready requires all changes to be committed"
[[ -t 0 ]] ||
    fail "--ready requires an interactive terminal"
command -v gh >/dev/null 2>&1 ||
    fail "GitHub CLI is required for --ready"

current_branch="$(git symbolic-ref --quiet --short HEAD)" ||
    fail "--ready does not support a detached HEAD"
repo_data="$(gh repo view --json nameWithOwner,isFork --jq '[.nameWithOwner, .isFork] | @tsv')"
IFS=$'\t' read -r repository is_fork <<<"$repo_data"
[[ "$is_fork" != "true" ]] ||
    fail "--ready does not support repositories cloned from forks"

pr_data="$(
    gh pr view "$current_branch" \
        --repo "$repository" \
        --json number,isDraft,isCrossRepository,baseRefName,headRefName,url \
        --jq '[.number, .isDraft, .isCrossRepository, .baseRefName, .headRefName, .url] | @tsv'
)" || fail "could not load a pull request for branch $current_branch"

IFS=$'\t' read -r pr_number is_draft is_cross_repository base_branch head_branch pr_url <<<"$pr_data"
[[ "$is_cross_repository" != "true" ]] ||
    fail "--ready does not support pull requests from forks"
[[ "$current_branch" == "$head_branch" ]] ||
    fail "current branch $current_branch does not match PR branch $head_branch"

git fetch --quiet origin "$base_branch"
base_sha="$(git rev-parse FETCH_HEAD)"

choose_tool
reviewed_head="$(git rev-parse HEAD)"
review_branch "$base_sha" "$base_branch"

[[ "$(git rev-parse HEAD)" == "$reviewed_head" && -z "$(git status --porcelain)" ]] ||
    fail "the working tree changed during the review; run the review again"

printf '\nReview completed with %s.\n' "$(reviewer_name)"
print_branch_summary "$base_sha" "$base_branch"

short_head="$(git rev-parse --short=10 "$reviewed_head")"
printf '\nNo changes have been pushed. Continue only if no actionable findings remain.\n'
if ! read -r -p "Push commit $short_head and mark PR #$pr_number ready? [y/N]: " confirmed; then
    fail "could not read confirmation"
fi
if [[ "$confirmed" != "y" && "$confirmed" != "Y" ]]; then
    printf 'Stopped; nothing was pushed.\n'
    exit 0
fi

printf '\nPublishing reviewed revision...\n'
git push origin "$reviewed_head:refs/heads/$head_branch"

gh api --method POST "repos/$repository/statuses/$reviewed_head" \
    -f state=success \
    -f context=local-code-review \
    -f description="Local $(reviewer_name) review completed" >/dev/null

if [[ "$is_draft" == "true" ]]; then
    gh pr ready "$pr_number" --repo "$repository" >/dev/null
    result="PR #$pr_number is ready for review."
else
    result="Review status updated for PR #$pr_number."
fi

printf '\n%s\nStatus: local-code-review (%s)\n%s\n' "$result" "$short_head" "$pr_url"
