namespace CatchIt.Core.Auth;

public record User(long Id, string Name, string Email, bool IsConfirmed);
