// <copyright file="ILambdaContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

/// <summary>
/// Object that allows you to access useful information available within
/// the Lambda execution environment.
/// </summary>
internal interface ILambdaContext
{
    // /// <summary>
    // /// Gets the AWS request ID associated with the request.
    // /// This is the same ID returned to the client that called invoke().
    // /// This ID is reused for retries on the same request.
    // /// </summary>
    // string AwsRequestId { get; }

    /// <summary>
    /// Gets information about the client application and device when invoked
    /// through the AWS Mobile SDK. It can be null.
    /// Client context provides client information such as client ID,
    /// application title, version name, version code, and the application
    /// package name.
    /// </summary>
    IClientContext ClientContext { get; }

    // /// <summary>
    // /// Gets name of the Lambda function that is running.
    // /// </summary>
    // string FunctionName { get; }
    //
    // /// <summary>
    // /// Gets the Lambda function version that is executing.
    // /// If an alias is used to invoke the function, then this will be
    // /// the version the alias points to.
    // /// </summary>
    // string FunctionVersion { get; }
    //
    // // /// <summary>
    // // /// Information about the Amazon Cognito identity provider when
    // // /// invoked through the AWS Mobile SDK.
    // // /// Can be null.
    // // /// </summary>
    // // ICognitoIdentity Identity { get; }
    //
    // /// <summary>
    // /// Gets the ARN used to invoke this function.
    // /// It can be function ARN or alias ARN.
    // /// An unqualified ARN executes the $LATEST version and aliases execute
    // /// the function version they are pointing to.
    // /// </summary>
    // string InvokedFunctionArn { get; }
    //
    // // /// <summary>
    // // /// Lambda logger associated with the Context object.
    // // /// </summary>
    // // ILambdaLogger Logger { get; }
    //
    // /// <summary>
    // /// Gets the CloudWatch log group name associated with the invoked function.
    // /// It can be null if the IAM user provided does not have permission for
    // /// CloudWatch actions.
    // /// </summary>
    // string LogGroupName { get; }
    //
    // /// <summary>
    // /// Gets the CloudWatch log stream name for this function execution.
    // /// It can be null if the IAM user provided does not have permission
    // /// for CloudWatch actions.
    // /// </summary>
    // string LogStreamName { get; }
    //
    // /// <summary>
    // /// Gets memory limit, in MB, you configured for the Lambda function.
    // /// </summary>
    // int MemoryLimitInMB { get; }

    // /// <summary>
    // /// Gets remaining execution time till the function will be terminated.
    // /// At the time you create the Lambda function you set maximum time
    // /// limit, at which time AWS Lambda will terminate the function
    // /// execution.
    // /// Information about the remaining time of function execution can be
    // /// used to specify function behavior when nearing the timeout.
    // /// </summary>
    // TimeSpan RemainingTime { get; }
}
