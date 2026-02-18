using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvdLauncher.Services;

public sealed class GraphService
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> GetDisplayNameAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=displayName,userPrincipalName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
        var upn = root.TryGetProperty("userPrincipalName", out var user) ? user.GetString() : null;

        return string.IsNullOrWhiteSpace(displayName) ? upn ?? "Signed-in user" : displayName!;
    }
}
