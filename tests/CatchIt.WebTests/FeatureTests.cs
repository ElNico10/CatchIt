using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CatchIt.WebTests;

/// <summary>Browser tests for every screen ported from the desktop app: accounts, product page, watchlist, dashboard, profile and admin.</summary>
public class FeatureTests(WebSite site) : IClassFixture<WebSite>
{
    private const string Password = "Passw0rd!x";

    private static ChromeDriver OpenBrowser()
    {
        var options = new ChromeOptions();
        options.AddArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage", "--window-size=1280,900");
        return new ChromeDriver(options);
    }

    private static IWebElement WaitFor(IWebDriver browser, string id) =>
        new WebDriverWait(browser, TimeSpan.FromSeconds(10)).Until(d => d.FindElements(By.Id(id)).FirstOrDefault());

    /// <summary>Waits until an element shows this text. A message from the previous click can still be on screen, so waiting for the element alone is not enough.</summary>
    private static void WaitForText(IWebDriver browser, string id, string text) =>
        new WebDriverWait(browser, TimeSpan.FromSeconds(10)).Until(d =>
        {
            try { return d.FindElements(By.Id(id)).FirstOrDefault()?.Text == text; }
            catch (StaleElementReferenceException) { return false; } // the page was replaced while reading it; try again
        });

    private static bool Exists(IWebDriver browser, string id) => browser.FindElements(By.Id(id)).Count > 0;

    private static void Type(IWebDriver browser, string id, string text)
    {
        var box = browser.FindElement(By.Id(id));
        box.Clear();
        box.SendKeys(text);
    }

    private static string NewEmail() => $"user{Guid.NewGuid():N}@catchit.test";

    private void LogIn(IWebDriver browser, string email, string password)
    {
        browser.Navigate().GoToUrl(site.Url + "/Login");
        Type(browser, "email", email);
        Type(browser, "password", password);
        browser.FindElement(By.Id("submit")).Click();
        WaitFor(browser, "who");
    }

    /// <summary>Registers a new account and confirms it with the code shown on the page; leaves the browser logged in.</summary>
    private string RegisterAndConfirm(IWebDriver browser, string name = "Test User")
    {
        var email = NewEmail();
        browser.Navigate().GoToUrl(site.Url + "/Register");
        Type(browser, "name", name);
        Type(browser, "email", email);
        Type(browser, "password", Password);
        browser.FindElement(By.Id("submit")).Click();

        var code = WaitFor(browser, "code").Text;
        Type(browser, "codebox", code);
        browser.FindElement(By.Id("confirm")).Click();
        WaitFor(browser, "confirmed");
        return email;
    }

    /// <summary>Searches for headphones and opens the first result, returning the product page.</summary>
    private void OpenFirstProduct(IWebDriver browser)
    {
        browser.Navigate().GoToUrl(site.Url + "/?q=headphones");
        WaitFor(browser, "results");
        browser.FindElement(By.CssSelector("#results tbody tr a")).Click();
        WaitFor(browser, "price");
    }

    [Fact]
    public void Registering_ShowsTheEmailCode_AndConfirmingClearsTheBanner()
    {
        using var browser = OpenBrowser();
        var email = NewEmail();
        browser.Navigate().GoToUrl(site.Url + "/Register");
        Type(browser, "email", email);
        Type(browser, "password", Password);
        browser.FindElement(By.Id("submit")).Click();

        Assert.Equal(6, WaitFor(browser, "code").Text.Length);
        Assert.Contains(email, browser.FindElement(By.Id("email-box")).Text);
        Assert.True(Exists(browser, "confirm-banner"));

        Type(browser, "codebox", "000000");
        browser.FindElement(By.Id("confirm")).Click();
        Assert.Equal(ConfirmationService.WrongCodeMessage, WaitFor(browser, "error").Text);

        Type(browser, "codebox", browser.FindElement(By.Id("code")).Text);
        browser.FindElement(By.Id("confirm")).Click();
        WaitFor(browser, "confirmed");
        Assert.False(Exists(browser, "confirm-banner"));
    }

    [Fact]
    public void RegisteringAnEmailTwice_IsRefused()
    {
        using var browser = OpenBrowser();
        var email = RegisterAndConfirm(browser);
        browser.FindElement(By.Id("logout")).Click();
        WaitFor(browser, "login-link");

        browser.Navigate().GoToUrl(site.Url + "/Register");
        Type(browser, "email", email);
        Type(browser, "password", Password);
        browser.FindElement(By.Id("submit")).Click();

        Assert.Equal(UserService.EmailTakenMessage, WaitFor(browser, "error").Text);
    }

    [Fact]
    public void AForgottenPassword_CanBeResetWithTheEmailedCode()
    {
        using var browser = OpenBrowser();
        var email = RegisterAndConfirm(browser);
        browser.FindElement(By.Id("logout")).Click();
        WaitFor(browser, "login-link");

        browser.Navigate().GoToUrl(site.Url + "/ForgotPassword");
        Type(browser, "email", email);
        browser.FindElement(By.Id("send")).Click();
        var code = WaitFor(browser, "code").Text;

        Type(browser, "codebox", code);
        Type(browser, "newpassword", "Brand-new-pw1");
        Type(browser, "confirmpassword", "Brand-new-pw1");
        browser.FindElement(By.Id("reset")).Click();
        WaitForText(browser, "message", PasswordResetService.PasswordResetMessage);

        LogIn(browser, email, "Brand-new-pw1");
    }

    [Fact]
    public void ResettingForAnUnknownEmail_SaysSo()
    {
        using var browser = OpenBrowser();
        browser.Navigate().GoToUrl(site.Url + "/ForgotPassword");
        Type(browser, "email", "nobody@catchit.test");
        browser.FindElement(By.Id("send")).Click();

        Assert.Equal(PasswordResetService.EmailNotFoundMessage, WaitFor(browser, "error").Text);
    }

    [Fact]
    public void TheProfile_CanBeRenamed_AndThePasswordChanged()
    {
        using var browser = OpenBrowser();
        var email = RegisterAndConfirm(browser, "Old Name");

        browser.Navigate().GoToUrl(site.Url + "/Profile");
        Type(browser, "name", "New Name");
        browser.FindElement(By.Id("save")).Click();
        Assert.Equal(ProfileService.SuccessMessage, WaitFor(browser, "profile-message").Text);
        Assert.Equal("New Name", browser.FindElement(By.Id("who")).Text);

        Type(browser, "current", "not-my-password");
        Type(browser, "new", "Another-pw-22");
        Type(browser, "again", "Another-pw-22");
        browser.FindElement(By.Id("change-password")).Click();
        Assert.Equal(UserService.CurrentPasswordIncorrectMessage, WaitFor(browser, "password-error").Text);

        Type(browser, "current", Password);
        Type(browser, "new", "Another-pw-22");
        Type(browser, "again", "Another-pw-22");
        browser.FindElement(By.Id("change-password")).Click();
        WaitForText(browser, "password-message", UserService.PasswordChangedMessage);

        browser.FindElement(By.Id("logout")).Click();
        WaitFor(browser, "login-link");
        LogIn(browser, email, "Another-pw-22");
    }

    [Fact]
    public void TheSearchFilters_NarrowTheResults()
    {
        using var browser = OpenBrowser();
        browser.Navigate().GoToUrl(site.Url + "/?q=headphones");
        var all = browser.FindElements(By.CssSelector("#results tbody tr")).Count;

        browser.Navigate().GoToUrl(site.Url + "/?q=headphones&tier=3&minRating=4.5");
        var status = WaitFor(browser, "status").Text;
        var filtered = browser.FindElements(By.CssSelector("#results tbody tr")).Count;

        Assert.True(all > 0);
        Assert.True(filtered < all, status);
    }

    [Fact]
    public void AProductPage_ShowsPriceFacts_Chart_AndAlternatives()
    {
        using var browser = OpenBrowser();
        OpenFirstProduct(browser);

        Assert.StartsWith("$", browser.FindElement(By.Id("price")).Text);
        Assert.Contains("Lowest ever", browser.FindElement(By.Id("lowest")).Text);
        Assert.Contains("90-day average", browser.FindElement(By.Id("average")).Text);
        Assert.True(Exists(browser, "chart") || Exists(browser, "no-chart"));
        Assert.True(Exists(browser, "alternatives") || Exists(browser, "no-alternatives"));
        Assert.True(Exists(browser, "login-hint")); // a guest cannot save to a watchlist
        Assert.False(Exists(browser, "watch"));
    }

    [Fact]
    public void TheChartRange_CanBeChanged()
    {
        using var browser = OpenBrowser();
        OpenFirstProduct(browser);
        var id = browser.Url.Split("id=")[1].Split('&')[0];

        browser.Navigate().GoToUrl($"{site.Url}/Product?id={id}&range=all");
        Assert.Contains("All", WaitFor(browser, "ranges").FindElement(By.TagName("strong")).Text);
        Assert.True(Exists(browser, "chart"));
    }

    [Fact]
    public void AGuest_IsSentToLogIn_ForAccountPages()
    {
        using var browser = OpenBrowser();
        foreach (var page in new[] { "Watchlist", "Dashboard", "Profile", "Admin" })
        {
            browser.Navigate().GoToUrl($"{site.Url}/{page}");
            Assert.Contains("/Login", browser.Url);
        }
    }

    [Fact]
    public void TheWatchlist_CanAddAProduct_SetATarget_ShowItOnTheDashboard_AndRemoveIt()
    {
        using var browser = OpenBrowser();
        RegisterAndConfirm(browser);
        OpenFirstProduct(browser);
        var product = browser.FindElement(By.Id("name")).Text.Replace(" On Sale", "");

        browser.FindElement(By.Id("watch")).Click();
        WaitForText(browser, "message", WatchlistService.AddedMessage);

        Type(browser, "target", "1000000");
        browser.FindElement(By.Id("save-target")).Click();
        WaitForText(browser, "message", WatchlistService.TargetSavedMessage);

        browser.Navigate().GoToUrl(site.Url + "/Watchlist");
        Assert.Contains(product, WaitFor(browser, "watchlist").Text);

        // A target of a million dollars is always reached, so the dashboard lists the product as below target.
        browser.Navigate().GoToUrl(site.Url + "/Dashboard");
        Assert.Contains(product, WaitFor(browser, "below").Text);

        browser.Navigate().GoToUrl(site.Url + "/Watchlist");
        browser.FindElement(By.CssSelector("button.remove")).Click();
        WaitForText(browser, "message", WatchlistService.RemovedMessage);
        Assert.True(Exists(browser, "empty"));
    }

    [Fact]
    public void AnEmptyDashboard_PointsToTheWatchlist()
    {
        using var browser = OpenBrowser();
        RegisterAndConfirm(browser);
        browser.Navigate().GoToUrl(site.Url + "/Dashboard");

        Assert.NotNull(WaitFor(browser, "empty"));
    }

    [Fact]
    public void AnOrdinaryUser_CannotUseTheAdminPage()
    {
        using var browser = OpenBrowser();
        RegisterAndConfirm(browser);
        browser.Navigate().GoToUrl(site.Url + "/Admin");

        Assert.False(Exists(browser, "nav-admin"));
        Assert.True(Exists(browser, "denied"));
        Assert.False(Exists(browser, "refresh"));
    }

    [Fact]
    public void AnAdministrator_CanRefreshPrices()
    {
        using var browser = OpenBrowser();
        LogIn(browser, Seeder.AdminEmail, Seeder.AdminPassword);
        browser.FindElement(By.Id("nav-admin")).Click();
        WaitFor(browser, "refresh").Click();

        Assert.StartsWith("Updated", WaitFor(browser, "result").Text);
        Assert.Contains("Last refresh", browser.FindElement(By.Id("last")).Text);
    }
}
