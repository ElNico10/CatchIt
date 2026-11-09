using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>Logs a user in with <see cref="UserService.Login"/> and stores the result in the login cookie (user story #3).</summary>
public class LoginModel(Database db) : PageModel
{
    /// <summary>The email typed into the form.</summary>
    [BindProperty] public string? Email { get; set; }

    /// <summary>The password typed into the form.</summary>
    [BindProperty] public string? Password { get; set; }

    /// <summary>Where to go after logging in; only local addresses are followed.</summary>
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    /// <summary>The message shown when the login failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Handles the form post.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        // A throwaway Session: the login result goes into the cookie, not into server memory.
        var result = new UserService(db, new Session()).Login(Email, Password);
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, WebApp.PrincipalFor(result.User!));
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
