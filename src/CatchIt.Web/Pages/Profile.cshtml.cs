using CatchIt.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>The logged-in user's name and email, and password change (user stories #16 and #17).</summary>
[Authorize]
public class ProfileModel(Session session, ProfileService profile, UserService users) : PageModel
{
    /// <summary>The display name.</summary>
    [BindProperty] public string? Name { get; set; }

    /// <summary>The email address.</summary>
    [BindProperty] public string? Email { get; set; }

    /// <summary>The current password, for a password change.</summary>
    [BindProperty] public string? CurrentPassword { get; set; }

    /// <summary>The new password.</summary>
    [BindProperty] public string? NewPassword { get; set; }

    /// <summary>The new password typed again.</summary>
    [BindProperty] public string? ConfirmPassword { get; set; }

    /// <summary>Success message for the profile form.</summary>
    public string? ProfileMessage { get; private set; }

    /// <summary>Error message for the profile form.</summary>
    public string? ProfileError { get; private set; }

    /// <summary>Success message for the password form.</summary>
    public string? PasswordMessage { get; private set; }

    /// <summary>Error message for the password form.</summary>
    public string? PasswordError { get; private set; }

    /// <summary>Fills the form with the current values.</summary>
    public void OnGet()
    {
        Name = session.CurrentUser!.Name;
        Email = session.CurrentUser.Email;
    }

    /// <summary>Saves the name and email, and rewrites the cookie so the page header shows the new name.</summary>
    public async Task<IActionResult> OnPostSaveAsync()
    {
        var result = profile.UpdateProfile(Name, Email);
        if (!result.Success)
        {
            ProfileError = result.Error;
            return Page();
        }

        await WebApp.SignInAsync(HttpContext, result.User!);
        ProfileMessage = ProfileService.SuccessMessage;
        Name = result.User!.Name;
        Email = result.User.Email;
        return Page();
    }

    /// <summary>Changes the password.</summary>
    public IActionResult OnPostPassword()
    {
        Name = session.CurrentUser!.Name;
        Email = session.CurrentUser.Email;
        var result = users.ChangePassword(CurrentPassword, NewPassword, ConfirmPassword);
        if (result.Success)
            PasswordMessage = UserService.PasswordChangedMessage;
        else
            PasswordError = result.Error;
        return Page();
    }
}
