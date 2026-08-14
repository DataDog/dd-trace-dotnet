using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;
using Xunit.Abstractions;

namespace Samples.Selenium;

public class TestSuite(ITestOutputHelper output) : IDisposable
{
    private readonly WebDriver _driver = CreateChromeDriverWithRetry(output);
    private readonly string _url = Environment.GetEnvironmentVariable("SAMPLES_SELENIUM_TEST_URL") ?? "http://localhost:62100/";

    private static WebDriver CreateChromeDriverWithRetry(ITestOutputHelper output)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromSeconds(5);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            WebDriver driver = null;
            try
            {
                driver = new ChromeDriver();
                return driver;
            }
            catch (Exception ex) when (ex is InvalidOperationException or WebDriverException)
            {
                driver?.Dispose();

                if (attempt == maxAttempts)
                {
                    throw;
                }

                output.WriteLine($"ChromeDriver initialization failed on attempt {attempt}/{maxAttempts}. Retrying in {delay.TotalSeconds} second(s). Error: {ex.Message}");
                Thread.Sleep(delay);
            }
        }

        throw new InvalidOperationException("Failed to initialize ChromeDriver.");
    }

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
