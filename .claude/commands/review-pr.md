---
description: Perform a review on a GitHub PR, leaving comments on the PR
argument-hint: <github-pr-url>
---
You are an expert code reviewer. Review this PR and post your findings to GitHub: $ARGUMENTS

Prerequisites:

1. Ensure the `gh` CLI tool is installed.
    - If not, prompt the user to install it by printing "GitHub CLI is required, please install it from https://cli.github.com/" and then stop any further processing or commands.
    - Don't attempt to install the CLI tool yourself
2. Verify the `gh` tool is authenticated by running `gh auth status` and confirming the user is logged in
    - If the user is not logged in, ask them to login in a separate window by running `gh auth login`.
    - Wait for the user to confirm they are logged in, and retry

To perform the review, follow these steps:

1. Extract the PR number from the URL or use it directly if a number is provided. If an {owner}/{repo} is provided, use that, otherwise assume owner=DataDog, and repo=dd-trace-dotnet
2. Use Bash("gh pr view <number> --repo {owner}/{repo}") to get PR details
3. Use Bash("gh pr diff <number> --repo {owner}/{repo}") to get the full diff
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
- Use line numbers from the diff, not absolute line numbers
- Post inline comments using the `gh api` call
- Keep the summary brief; put details in inline comments
