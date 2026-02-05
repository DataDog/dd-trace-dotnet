---
name: review-pr
description: Perform a review on a GitHub PR, leaving comments on the PR
argument-hint: <pr-number-or-url>
disable-model-invocation: true
allowed-tools: Bash(gh pr view:*), Bash(gh pr diff:*), Bash(gh pr comment:*)
context: fork
agent: general-purpose
---

# Review GitHub PR

You are an expert code reviewer. Review this PR and post your findings to GitHub: $ARGUMENTS

Prerequisites:

1. Ensure the `gh` CLI tool is installed.
    - If not, prompt the user to install it by printing "GitHub CLI is required, please install it from https://cli.github.com/" and then stop any further processing or commands.
    - Don't attempt to install the CLI tool yourself
2. Verify the `gh` tool is authenticated by running `gh auth status` and confirming the user is logged in
    - If the user is not logged in, ask them to login in a separate window by running `gh auth login`.
    - Wait for the user to confirm they are logged in, and retry

To perform the review, follow these steps:

1. If $1 is a number use that as the PR number and assume owner=DataDog, and repo=dd-trace-dotnet. Otherwise, verify $1 is a github URL for this repo (it starts https://github.com/DataDog/dd-trace-dotnet), and extract the PR number
2. Run `gh pr view <number> --repo {owner}/{repo} --json title,body,headRefOid,author,files` to get PR details
3. Run `gh pr diff <number> --repo {owner}/{repo}` to get the full diff
4. Analyze the changes thoroughly, identifying:
   - Specific issues with file paths and line numbers
   - Potential bugs or risks
   - Code quality concerns
   - Performance implications
   - Security considerations

5. Post your review to GitHub in TWO parts:

   **Part A: Inline File Comments**
   - For each specific issue you found, create a GitHub API call
   - Use the format: `gh api -X POST -H "Accept: application/vnd.github+json" "repos/{owner}/{repo}/pulls/{pull_number}/comments" -f body="Your file-level comment" -f path="path/to/file" -F line=<line_number> -f side="RIGHT" -f commit_id="<commit_sha>"`
     - The commit_sha should be the head commit SHA from the PR (use headRefOid from the PR details JSON)
   - Each comment should:
     - Reference the specific file and line number
     - Explain the issue clearly
     - Provide a concrete suggestion or fix
     - Be constructive and actionable
   - Add suffix to each: "🤖 Claude Code"

   **Part B: Brief Summary Comment**
   - Use `gh pr comment <number> --repo {owner}/{repo} --body "..."` to post a brief summary (1-2 paragraphs max)
   - Include: overall assessment, key findings. Don't provide a recommendation on approve/request changes/comment.
   - Add suffix: "Review by 🤖 Claude Code"

IMPORTANT:
- Focus on specific, actionable issues - not general praise
- Every issue must have a file path and line number
- Use the line number from the new version of the file (i.e., the line number you'd see if you opened the file after the PR is merged), which corresponds to the line parameter in the GitHub API.
- Post inline comments using the `gh api` call
- Keep the summary brief; put details in inline comments
