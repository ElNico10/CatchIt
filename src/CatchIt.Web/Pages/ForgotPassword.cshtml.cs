using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CatchIt.Core.Auth;

namespace CatchIt.Web.Pages;

/// <summary>Resets a forgotten password with a code from the (simulated) email.</summary>
public class ForgotPasswordModel(WebCodes codes) : PageModel
{
    /// <summary>The account's email.</summary>
    [BindProperty] public string? Email { get; set; }

    /// <summary>The code from the email.</summary>
    [BindProperty] public string? Code { get; set; }

    /// <summary>The new password.</summary>
    [BindProperty] public string? NewPassword { get; set; }

    /// <summary>The new password typed again.</summary>
    [BindProperty] public string? ConfirmPassword { get; set; }

    /// <summary>The email that was "sent", or null before a code is requested.</summary>
    public SimulatedEmail? Mail { get; private set; }

    /// <summary>A success message.</summary>
    public string? Message { get; private set; }

    /// <summary>The message shown after a failed attempt.</summary>
    public string? Error { get; private set; }

    /// <summary>The "send me a code" button.</summary>
    public IActionResult OnPostSend()
    {
        var result = codes.RequestReset(Email);
        if (result.Success)
            Message = PasswordResetService.CodeSentMessage;
        else
            Error = result.Error;
        Mail = codes.PendingReset(Email);
        return Page();
    }

    /// <summary>The "reset password" button.</summary>
    public IActionResult OnPostReset()
    {
        var result = codes.ResetPassword(Email, Code, NewPassword, ConfirmPassword);
        if (result.Success)
        {
            Message = PasswordResetService.PasswordResetMessage;
            return Page();
        }
        Error = result.Error;
        Mail = codes.PendingReset(Email);
        return Page();
    }
}
