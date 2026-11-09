using System.Security.Claims;
using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CatchIt.Web;

/// <summary>Builds the CatchIt web server: the same <c>CatchIt.Core</c> services as the desktop app, behind Razor Pages.</summary>
public static class WebApp
{
    /// <summary>Claim holding "1" when the logged-in user has confirmed their account.</summary>
    public const string ConfirmedClaim = "confirmed";

    /// <summary>Role claim value for administrators.</summary>
    public const string AdminRole = "admin";

    /// <summary>Creates the (not yet started) web application.</summary>
    /// <param name="args">Command-line arguments passed to ASP.NET Core, such as <c>--urls</c>.</param>
    /// <param name="dbPath">The SQLite file to serve. Defaults to the <c>CATCHIT_DB</c> environment variable, then the desktop app's path.</param>
    public static WebApplication Create(string[] args, string? dbPath = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        var db = new Database(dbPath ?? Environment.GetEnvironmentVariable("CATCHIT_DB") ?? Database.DefaultPath);
        db.Initialize();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton(new ListingSearchService(db));

        // One Session per request, rebuilt from the login cookie. Visitors without a cookie browse as guests,
        // so nobody is forced to log in to search.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped(sp => SessionFor(sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.User));
        builder.Services.AddScoped<UserService>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/Login";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                // shortcut: the admin flag is copied into the cookie at login, so a demotion only takes effect at the next login.
            });
        // The explicit part lets a test project host this app: the pages live here, not in the entry assembly.
        builder.Services.AddRazorPages().AddApplicationPart(typeof(WebApp).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        return app;
    }

    /// <summary>The claims stored in the login cookie for a user.</summary>
    public static ClaimsPrincipal PrincipalFor(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ConfirmedClaim, user.IsConfirmed ? "1" : "0"),
        };
        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static Session SessionFor(ClaimsPrincipal principal)
    {
        var session = new Session();
        if (principal.Identity?.IsAuthenticated == true && long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            session.SignIn(new User(id, principal.FindFirstValue(ClaimTypes.Name) ?? "", principal.FindFirstValue(ClaimTypes.Email) ?? "",
                principal.FindFirstValue(ConfirmedClaim) == "1", principal.IsInRole(AdminRole)));
        }
        else
        {
            session.EnterGuestMode();
        }
        return session;
    }
}
