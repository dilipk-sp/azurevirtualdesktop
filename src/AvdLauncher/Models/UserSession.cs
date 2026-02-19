using Microsoft.Identity.Client;

namespace AvdLauncher.Models;

public sealed class UserSession
{
    public string DisplayName { get; set; } = string.Empty;
    public string Upn { get; set; } = string.Empty;
    public IAccount MsalAccount { get; set; } = default!;

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Upn : $"{DisplayName} ({Upn})";
}
