using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>Ends the login (user story #17). POST only, so a link on another site cannot log someone out.</summary>
public class LogoutModel : PageModel
{
    /// <summary>A plain visit just goes back to the search page.</summary>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Removes the login cookie.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
