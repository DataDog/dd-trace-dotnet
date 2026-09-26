// <copyright file="LocalizedStackTraceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Debugger.IntegrationTests
{
    public class LocalizedStackTraceTests
    {
        [Fact]
        public void ExceptionNormalizer_ParseFrames_JapaneseStackTrace_ParsesUserFrames()
        {
            var exceptionString =
                "System.InvalidOperationException: エラーです。\r\n" +
                "   場所 MyApp.Services.UserService.GetUserById(Int32 id) 場所 C:\\Projects\\MyApp\\Services\\UserService.cs:行 45\r\n" +
                "   場所 lambda_method(Closure, Object, Object[])\r\n" +
                "   場所 Datadog.Trace.SomeType.SomeMethod() 場所 C:\\dd\\SomeType.cs:行 10\r\n" +
                "   場所 MyApp.Controllers.UserController.GetUser(Int32 id) 場所 C:\\Projects\\MyApp\\Controllers\\UserController.cs:行 25";

            var normalizer = new TestExceptionNormalizer();
            var frames = normalizer.ParseFrames(exceptionString);

            frames.Should().Equal(
                "MyApp.Services.UserService.GetUserById(Int32 id)",
                "MyApp.Controllers.UserController.GetUser(Int32 id)");
        }

        [Fact]
        public void StackTraceProcessor_ParseFrames_JapaneseStackTrace_ParsesFrames()
        {
            var exceptionString =
                "System.InvalidOperationException: エラーです。\r\n" +
                "   場所 MyApp.Services.UserService.GetUserById(Int32 id) 場所 C:\\Projects\\MyApp\\Services\\UserService.cs:行 45\r\n" +
                "   場所 MyApp.Controllers.UserController.GetUser(Int32 id) 場所 C:\\Projects\\MyApp\\Controllers\\UserController.cs:行 25";

            var frames = StackTraceProcessor.ParseFrames(exceptionString);

            frames.Should().Equal(
                "MyApp.Services.UserService.GetUserById(Int32 id)",
                "MyApp.Controllers.UserController.GetUser(Int32 id)");
        }

        [Fact]
        public void ExceptionNormalizer_NormalizeAndHashException_JapaneseStackTrace_IgnoresMessagesAndLineNumbers()
        {
            var exceptionString1 =
                "System.InvalidOperationException: 最初のメッセージです。\r\n" +
                "   場所 MyApp.Services.UserService.GetUserById(Int32 id) 場所 C:\\Projects\\MyApp\\Services\\UserService.cs:行 45\r\n" +
                "   場所 MyApp.Controllers.UserController.GetUser(Int32 id) 場所 C:\\Projects\\MyApp\\Controllers\\UserController.cs:行 25";

            var exceptionString2 =
                "System.InvalidOperationException: 別のメッセージです。\r\n" +
                "   場所 MyApp.Services.UserService.GetUserById(Int32 id) 場所 D:\\Source\\MyApp\\Services\\UserService.cs:行 999\r\n" +
                "   場所 MyApp.Controllers.UserController.GetUser(Int32 id) 場所 D:\\Source\\MyApp\\Controllers\\UserController.cs:行 777";

            var normalizer = new TestExceptionNormalizer();
            var hash1 = normalizer.NormalizeAndHashException(exceptionString1, "System.InvalidOperationException", null, new StringBuilder());
            var hash2 = normalizer.NormalizeAndHashException(exceptionString2, "System.InvalidOperationException", null, new StringBuilder());

            hash1.Should().Be(hash2);
        }
    }
}
