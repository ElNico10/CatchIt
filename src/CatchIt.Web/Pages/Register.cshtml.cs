using CatchIt.Core.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>Creates an account (user story #1), logs the new user in, and sends them to confirm it.</summary>
public class RegisterModel(UserService users, WebCodes codes) : PageModel
{
    /// <summary>The display name typed into the form (optional).</summary>
    [BindProperty] public string? Name { get; set; }

    /// <summary>The email typed into the form.</summary>
    [BindProperty] public string? Email { get; set; }

    /// <summary>The password typed into the form.</summary>
    [BindProperty] public string? Password { get; set; }

    /// <summary>The message shown when registration failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Handles the form post.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        var result = users.Register(Email, Password, Name ?? "");
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        await WebApp.SignInAsync(HttpContext, result.User!);
        codes.RequestConfirmation(result.User!);
        return RedirectToPage("/Confirm");
    }
}
