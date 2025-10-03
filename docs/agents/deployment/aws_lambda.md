# AWS Lambda

**Supported runtimes:** .NET Core 3.1, .NET 6, .NET 7, .NET 8

## Installation Methods

1. **Datadog CLI**: Quickest method; modifies Lambda configuration without redeployment
2. **Serverless Framework Plugin**: Auto-configures functions via `serverless-plugin-datadog`
3. **AWS SAM**: Uses Datadog CloudFormation macro to transform templates
4. **Container Image**: Add Datadog Extension and tracer to container image
5. **Terraform**: Use `lambda-datadog` module wrapping `aws_lambda_function`
6. **Custom/Manual**: Configure Lambda layers and environment variables directly

## Key Environment Variables

- `AWS_LAMBDA_EXEC_WRAPPER=/opt/datadog_wrapper` — Required for tracer initialization
- `DD_SITE` — Datadog site (e.g., `datadoghq.com`, `datadoghq.eu`)
- `DD_API_KEY_SECRET_ARN` — ARN of AWS secret containing Datadog API key (plaintext string)
- `DD_API_KEY` — Alternative for testing; API key in plaintext (not recommended for production)

## Lambda Layers

### Tracer Layer
`arn:aws:lambda:<region>:464622532012:layer:dd-trace-dotnet:<version>`
- ARM64: `dd-trace-dotnet-ARM`
- GovCloud: Account `002406178527`

### Extension Layer
`arn:aws:lambda:<region>:464622532012:layer:Datadog-Extension:<version>`
- ARM64: `Datadog-Extension-ARM`
- GovCloud: Account `002406178527`

## Container Images

```dockerfile
# Add Lambda Extension
COPY --from=public.ecr.aws/datadog/lambda-extension:<TAG> /opt/. /opt/

# Install .NET APM client
RUN yum -y install tar wget gzip
RUN wget https://github.com/DataDog/dd-trace-dotnet/releases/download/v<TRACER_VERSION>/datadog-dotnet-apm-<TRACER_VERSION>.tar.gz
RUN mkdir /opt/datadog
RUN tar -C /opt/datadog -xzf datadog-dotnet-apm-<TRACER_VERSION>.tar.gz
ENV AWS_LAMBDA_EXEC_WRAPPER /opt/datadog_wrapper
```
