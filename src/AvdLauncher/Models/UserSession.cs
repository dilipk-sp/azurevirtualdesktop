using Microsoft.Identity.Client;

namespace AvdLauncher.Models;

public sealed class UserSession
{
    public string DisplayName { get; init; } = string.Empty;
    public string Upn { get; init; } = string.Empty;
    public IAccount MsalAccount { get; init; } = default!;

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Upn : $"{DisplayName} ({Upn})";
}
