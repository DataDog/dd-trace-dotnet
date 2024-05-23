using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;
using Xunit.Abstractions;

namespace Samples.Selenium;

public class TestSuite(ITestOutputHelper output) : IDisposable
{
    private readonly WebDriver _driver = new ChromeDriver();
    private readonly string _url = Environment.GetEnvironmentVariable("SAMPLES_SELENIUM_TEST_URL") ?? "http://localhost:62100/";

    [Fact]
    public void SeleniumTest()
    {
        var browserName = _driver.Capabilities.GetCapability("browserName");
        output.WriteLine(browserName.ToString());
        var browserVersion = _driver.Capabilities.GetCapability("browserVersion") ??
                             _driver.Capabilities.GetCapability("version");
        output.WriteLine(browserVersion.ToString());
        var navigate = _driver.Navigate();
        var manage = _driver.Manage();
        navigate.GoToUrl(_url);
        foreach (var cookie in manage.Cookies.AllCookies)
        {
            output.WriteLine("{0} = {1}", cookie.Name, cookie.Value);
        }

        output.WriteLine(_driver.Title);
        _driver.Close();
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }
}
