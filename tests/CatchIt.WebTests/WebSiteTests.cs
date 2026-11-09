using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Web;
using Microsoft.AspNetCore.Builder;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CatchIt.WebTests;

/// <summary>Starts the real web server on a free port with a freshly seeded temporary database, once for all tests in the class.</summary>
public sealed class WebSite : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "catchit-web-" + Guid.NewGuid().ToString("N"));
    private WebApplication? _app;

    /// <summary>The address the server is listening on, such as <c>http://127.0.0.1:5123</c>.</summary>
    public string Url { get; private set; } = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "catchit.db");
        new Seeder(new Database(path)).Run();

        _app = WebApp.Create(["--urls", "http://127.0.0.1:0"], path);
        await _app.StartAsync();
        Url = _app.Urls.First();
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
            await _app.DisposeAsync();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* a temp file still locked is not worth failing the run */ }
    }
}

/// <summary>Browser tests (Selenium + headless Chrome) against the running web app.</summary>
public class WebSiteTests(WebSite site) : IClassFixture<WebSite>
{
    private const string DemoEmail = "demo@catchit.test";
    private const string DemoPassword = "Demo1234!";

    private static ChromeDriver OpenBrowser()
    {
        var options = new ChromeOptions();
        // --no-sandbox and --disable-dev-shm-usage are needed when Chrome runs as root or in a small container.
        options.AddArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage", "--window-size=1280,900");
        return new ChromeDriver(options);
    }

    /// <summary>Waits for an element to appear. A click that submits a form does not wait for the next page, so every test waits for what it expects.</summary>
    private static IWebElement WaitFor(IWebDriver browser, string id) =>
        new WebDriverWait(browser, TimeSpan.FromSeconds(10)).Until(d => d.FindElements(By.Id(id)).FirstOrDefault());

    private static bool Exists(IWebDriver browser, string id) => browser.FindElements(By.Id(id)).Count > 0;

    private void LogIn(IWebDriver browser, string email, string password)
    {
        browser.Navigate().GoToUrl(site.Url + "/Login");
        browser.FindElement(By.Id("email")).SendKeys(email);
        browser.FindElement(By.Id("password")).SendKeys(password);
        browser.FindElement(By.Id("submit")).Click();
    }

    [Fact]
    public void AGuest_CanSearch_AndSeesTheSimulatedDataNotice()
    {
        using var browser = OpenBrowser();
        browser.Navigate().GoToUrl(site.Url);

        browser.FindElement(By.Id("q")).SendKeys("headphones");
        browser.FindElement(By.Id("search")).Click();
        WaitFor(browser, "results");

        Assert.NotEmpty(browser.FindElements(By.CssSelector("#results tbody tr")));
        Assert.Contains("simulated", browser.FindElement(By.Id("disclaimer")).Text);
        Assert.True(Exists(browser, "login-link"));
        Assert.False(Exists(browser, "who"));
    }

    [Fact]
    public void ASearchWithNoMatch_SaysSo()
    {
        using var browser = OpenBrowser();
        browser.Navigate().GoToUrl(site.Url + "/?q=zzzzqqqq");

        Assert.Equal("No products found", WaitFor(browser, "status").Text);
        Assert.False(Exists(browser, "results"));
    }

    [Fact]
    public void LoggingIn_ShowsTheUser_AndLoggingOutRemovesThem()
    {
        using var browser = OpenBrowser();

        LogIn(browser, DemoEmail, DemoPassword);
        Assert.Equal("Demo User", WaitFor(browser, "who").Text);

        browser.FindElement(By.Id("logout")).Click();
        WaitFor(browser, "login-link");
        Assert.False(Exists(browser, "who"));
    }

    [Fact]
    public void AWrongPassword_ShowsAnErrorAndDoesNotLogIn()
    {
        using var browser = OpenBrowser();

        LogIn(browser, DemoEmail, "wrong-password");

        Assert.Equal(UserService.InvalidLoginMessage, WaitFor(browser, "error").Text);
        Assert.False(Exists(browser, "who"));
    }
}
