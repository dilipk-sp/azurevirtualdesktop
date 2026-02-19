using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AvdLauncher.Models;

namespace AvdLauncher.Services;

public sealed class AvdService
{
    private readonly HttpClient _httpClient = new();

    public async Task<IReadOnlyList<RemoteAppInfo>> GetAssignedRemoteAppsAsync(
        string accessToken,
        string subscriptionId,
        string resourceGroup)
    {
        var appGroups = await GetApplicationGroupsAsync(accessToken, subscriptionId, resourceGroup).ConfigureAwait(false);
        var workspaces = await GetWorkspacesAsync(accessToken, subscriptionId, resourceGroup).ConfigureAwait(false);

        var apps = new List<RemoteAppInfo>();
        foreach (var appGroup in appGroups)
        {
            var appGroupName = appGroup.Name;
            var workspaceName = workspaces.FirstOrDefault(w => w.ApplicationGroupIds.Contains(appGroup.Id))?.Name ?? "Not linked";

            var appList = await GetApplicationsAsync(accessToken, subscriptionId, resourceGroup, appGroupName).ConfigureAwait(false);
            apps.AddRange(appList.Select(app => new RemoteAppInfo
            {
                Name = app.Name,
                WorkspaceName = workspaceName,
                ApplicationGroupName = appGroupName,
                HostPoolName = app.HostPoolName,
                AppId = app.AppId,
                LaunchUri = BuildWindowsAppLaunchUri(workspaceName)
            }));
        }

        return apps.OrderBy(a => a.WorkspaceName).ThenBy(a => a.ApplicationGroupName).ThenBy(a => a.Name).ToList();
    }

    private async Task<List<(string Id, string Name)>> GetApplicationGroupsAsync(string token, string sub, string rg)
    {
        var url = $"https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DesktopVirtualization/applicationGroups?api-version=2023-09-05";
        var root = await GetResourceAsync(token, url).ConfigureAwait(false);

        var list = new List<(string, string)>();
        foreach (var item in root.GetProperty("value").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? string.Empty;
            var name = item.GetProperty("name").GetString() ?? string.Empty;
            list.Add((id, name));
        }

        return list;
    }

    private async Task<List<(string Name, HashSet<string> ApplicationGroupIds)>> GetWorkspacesAsync(string token, string sub, string rg)
    {
        var url = $"https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DesktopVirtualization/workspaces?api-version=2023-09-05";
        var root = await GetResourceAsync(token, url).ConfigureAwait(false);

        var list = new List<(string, HashSet<string>)>();
        foreach (var item in root.GetProperty("value").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString() ?? string.Empty;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item.GetProperty("properties").TryGetProperty("applicationGroupReferences", out var refs))
            {
                foreach (var id in refs.EnumerateArray())
                {
                    var value = id.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ids.Add(value);
                    }
                }
            }

            list.Add((name, ids));
        }

        return list;
    }

    private async Task<List<(string Name, string HostPoolName, string? AppId)>> GetApplicationsAsync(string token, string sub, string rg, string appGroupName)
    {
        var encodedGroup = Uri.EscapeDataString(appGroupName);
        var url = $"https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DesktopVirtualization/applicationGroups/{encodedGroup}/applications?api-version=2023-09-05";
        var root = await GetResourceAsync(token, url).ConfigureAwait(false);

        var list = new List<(string, string, string?)>();
        foreach (var item in root.GetProperty("value").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString() ?? string.Empty;
            var properties = item.GetProperty("properties");
            var hostPoolArmPath = properties.TryGetProperty("hostPoolArmPath", out var hp) ? hp.GetString() : null;
            var appId = properties.TryGetProperty("appAlias", out var alias) ? alias.GetString() : null;
            list.Add((name, ExtractName(hostPoolArmPath), appId));
        }

        return list;
    }

    private async Task<JsonElement> GetResourceAsync(string token, string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string ExtractName(string? armPath)
    {
        if (string.IsNullOrWhiteSpace(armPath))
        {
            return "Unknown";
        }

        var parts = armPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.LastOrDefault() ?? armPath;
    }

    private static string BuildWindowsAppLaunchUri(string workspaceName)
    {
        _ = workspaceName;
        return "https://learn.microsoft.com/en-us/azure/virtual-desktop/uri-scheme";
    }
}
