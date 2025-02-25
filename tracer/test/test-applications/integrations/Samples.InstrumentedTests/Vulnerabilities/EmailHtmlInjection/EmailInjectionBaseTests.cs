using System;
using System.Net.Mail;
using Amazon.Runtime;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailInjectionBaseTests : InstrumentationTestsBase
{
    protected static string taintedName = "Alice<h1>Hi</h1>";
    protected static string untaintedName = "Peter";
    protected static string taintedLastName = "Stevens";
    protected static string untaintedLastName = "Smith";
    protected static string emailHtmlInjectionType = "EMAIL_HTML_INJECTION";

    public EmailInjectionBaseTests()
    {
        AddTainted(taintedName);
        AddTainted(taintedLastName);
    }

    protected static string GetContent(string name, string lastName)
    {
        return "Hi " + name + " " + lastName + "!";
    }

    protected void TestMailCall(Action expression, bool isVulnerable)
    {
        TestEmailSendCall(expression);

        if (isVulnerable)
        {
            AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice<h1>Hi</h1>-+: :+-Stevens-+:!");
        }
        else
        {
            AssertNotVulnerable();
        }
    }

    // This method is used to test the email send call. It catches the expected exceptions.
    protected void TestEmailSendCall(Action expression)
    {
        try
        {
            expression.Invoke();
        }
        catch (SmtpException) { }
        catch (InvalidOperationException) { }
        catch (AmazonClientException) { }
    }
}
