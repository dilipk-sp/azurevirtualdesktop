namespace AvdLauncher.Models;

public sealed class RemoteAppInfo
{
    public string Name { get; init; } = string.Empty;
    public string WorkspaceName { get; init; } = string.Empty;
    public string ApplicationGroupName { get; init; } = string.Empty;
    public string HostPoolName { get; init; } = string.Empty;
    public string? LaunchUri { get; init; }
    public string? AppId { get; init; }
}
