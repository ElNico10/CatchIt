namespace CatchIt.Core.Auth;

/// <summary>A registered account.</summary>
/// <param name="IsAdmin">True for administrators, who can use admin-only features such as the price refresh.</param>
public record User(long Id, string Name, string Email, bool IsConfirmed, bool IsAdmin = false);
