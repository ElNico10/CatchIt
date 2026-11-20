using System.Security.Claims;
using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Pricing;
using CatchIt.Core.Search;
using Microsoft.AspNetCore.Authentication;
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
        builder.Services.AddSingleton(new ProductDetailService(db));
        builder.Services.AddSingleton(new WebCodes(db));

        // One Session per request, rebuilt from the login cookie. Visitors without a cookie browse as guests,
        // so nobody is forced to log in to search.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped(sp => SessionFor(sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.User));
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<ProfileService>();
        builder.Services.AddScoped<WatchlistService>();
        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddScoped<PriceRefreshService>();

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
        app.MapGet("/health", Health);
        return app;
    }

    /// <summary>
    /// <c>GET /health</c>: proves the app is up and can read its database (issue #26). Answers 200 with the
    /// number of products and the running image's version, or 503 when the database has no products. Render
    /// uses it as the service's health check, and <c>scripts/smoke-test.sh</c> uses it to wait for a new deploy.
    /// </summary>
    private static IResult Health(Database db)
    {
        long products;
        using (var conn = db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Products";
            products = Convert.ToInt64(cmd.ExecuteScalar());
        }

        // APP_VERSION is baked into the Docker image by the publish workflow: it is the image tag (short commit SHA).
        var version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "local";
        var body = new { status = products > 0 ? "ok" : "empty database", version, products };
        return products > 0 ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Writes (or rewrites) the login cookie, for example after the user confirms their account or changes their profile.</summary>
    public static Task SignInAsync(HttpContext context, User user)
    {
        // The cookie only reaches the next request, so also update this request's user: the page rendered in the
        // same response (header name, confirmation banner) then already shows the change.
        context.User = PrincipalFor(user);
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, context.User);
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
