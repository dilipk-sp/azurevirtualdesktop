namespace AvdLauncher.Models;

public sealed class RemoteAppInfo
{
    public string Name { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string ApplicationGroupName { get; set; } = string.Empty;
    public string HostPoolName { get; set; } = string.Empty;
    public string? LaunchUri { get; set; }
    public string? AppId { get; set; }
}
