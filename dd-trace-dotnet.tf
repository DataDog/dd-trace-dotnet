# CI Identities IAM Policy for CI jobs in the dd-trace-dotnet repository

# Policy for trusted jobs (protected branches)
data "aws_iam_policy_document" "dd_trace_dotnet_trusted_jobs" {
  statement {
    sid    = "AllowWritingWindowsFilterBuildsTracer"
    effect = "Allow"
    actions = [
      "s3:PutObject",
      "s3:PutObjectAcl"
    ]
    resources = [
      "arn:aws:s3:::dd-windowsfilter/builds/tracer/*",
    ]
  }

  statement {
    sid    = "AllowReadingCiVisibilityApiKey"
    effect = "Allow"
    actions = [
      "ssm:GetParameter",
    ]
    resources = [
      "arn:aws:ssm:us-east-1:486234852809:parameter/ci.dd-trace-dotnet.dd_api_key-prod",
    ]
  }
}

# Policy for untrusted jobs (non-protected branches)
# TODO: After onboarding is complete, reduce permissions for untrusted jobs
# For now, they get the same permissions as trusted jobs
data "aws_iam_policy_document" "dd_trace_dotnet_untrusted_jobs" {
  source_policy_documents = [data.aws_iam_policy_document.dd_trace_dotnet_trusted_jobs.json]
}
