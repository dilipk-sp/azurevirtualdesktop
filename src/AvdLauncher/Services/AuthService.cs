using System;
using System.Linq;
using System.Threading.Tasks;
using AvdLauncher.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace AvdLauncher.Services;

public sealed class AuthService
{
    private IPublicClientApplication? _pca;
    private string? _tenantId;
    private string? _clientId;

    private static readonly string[] Scopes =
    {
        "https://graph.microsoft.com/User.Read",
        "https://management.azure.com/user_impersonation"
    };

    public void Configure(string tenantId, string clientId)
    {
        if (_pca is not null && string.Equals(_tenantId, tenantId, StringComparison.OrdinalIgnoreCase) && string.Equals(_clientId, clientId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pca = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithDefaultRedirectUri()
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                Title = "Sign in to Azure Virtual Desktop Launcher"
            })
            .Build();

        _tenantId = tenantId;
        _clientId = clientId;
    }

    public async Task<UserSession> AddUserInteractiveAsync()
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        var result = await _pca.AcquireTokenInteractive(Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return new UserSession
        {
            DisplayName = result.Account?.Username ?? "Signed-in user",
            Upn = result.Account?.Username ?? string.Empty,
            MsalAccount = result.Account!
        };
    }

    public async Task<string> GetAccessTokenAsync(UserSession session)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        try
        {
            var silentResult = await _pca.AcquireTokenSilent(Scopes, session.MsalAccount)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return silentResult.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            var interactiveResult = await _pca.AcquireTokenInteractive(Scopes)
                .WithAccount(session.MsalAccount)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return interactiveResult.AccessToken;
        }
    }

    public async Task<UserSession[]> GetKnownUsersAsync()
    {
        if (_pca is null)
        {
            return Array.Empty<UserSession>();
        }

        var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
        return accounts.Select(a => new UserSession
        {
            DisplayName = a.Username,
            Upn = a.Username,
            MsalAccount = a
        }).ToArray();
    }

    public async Task SignOutAsync(UserSession session)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        await _pca.RemoveAsync(session.MsalAccount).ConfigureAwait(false);
    }
}
