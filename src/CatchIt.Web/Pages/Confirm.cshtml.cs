using CatchIt.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>Confirms the logged-in user's account with the code from the (simulated) email (user story #2).</summary>
[Authorize]
public class ConfirmModel(Session session, WebCodes codes) : PageModel
{
    /// <summary>The code typed into the form.</summary>
    [BindProperty] public string? Code { get; set; }

    /// <summary>True once the account is confirmed.</summary>
    public bool IsConfirmed => session.CurrentUser?.IsConfirmed == true;

    /// <summary>The email that was "sent", or null when no code is waiting.</summary>
    public SimulatedEmail? Email { get; private set; }

    /// <summary>The message shown after a failed attempt.</summary>
    public string? Error { get; private set; }

    /// <summary>Shows the waiting email, if there is one.</summary>
    public void OnGet() => Email = codes.PendingConfirmation(session.CurrentUser!.Id);

    /// <summary>The "send me a code" button: creates a new code and replaces any earlier one.</summary>
    public IActionResult OnPostSend()
    {
        var result = codes.RequestConfirmation(session.CurrentUser!);
        if (!result.Success)
            Error = result.Error;
        Email = codes.PendingConfirmation(session.CurrentUser!.Id);
        return Page();
    }

    /// <summary>The confirm button: checks the code and, if correct, refreshes the login cookie.</summary>
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        var result = codes.Confirm(session.CurrentUser!, Code);
        if (!result.Success)
        {
            Error = result.Error;
            Email = codes.PendingConfirmation(session.CurrentUser!.Id);
            return Page();
        }

        await WebApp.SignInAsync(HttpContext, result.User!);
        return RedirectToPage("/Confirm");
    }
}
