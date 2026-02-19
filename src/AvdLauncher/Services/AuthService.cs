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
    private bool _useBroker;

    private static readonly string[] GraphScopes =
    {
        "https://graph.microsoft.com/User.Read"
    };

    private static readonly string[] ManagementScopes =
    {
        "https://management.azure.com/user_impersonation"
    };

    public void Configure(string tenantId, string clientId, bool useBroker = true)
    {
        if (_pca is not null
            && string.Equals(_tenantId, tenantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_clientId, clientId, StringComparison.OrdinalIgnoreCase)
            && _useBroker == useBroker)
        {
            return;
        }

        _pca = BuildClient(tenantId, clientId, useBroker);
        _tenantId = tenantId;
        _clientId = clientId;
        _useBroker = useBroker;
    }

    public async Task<UserSession> AddUserInteractiveAsync(IntPtr parentWindowHandle)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        try
        {
            var result = await _pca.AcquireTokenInteractive(GraphScopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(parentWindowHandle)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return new UserSession
            {
                DisplayName = result.Account?.Username ?? "Signed-in user",
                Upn = result.Account?.Username ?? string.Empty,
                MsalAccount = result.Account!
            };
        }
        catch (MsalClientException ex) when (_useBroker && IsBrokerConfigurationError(ex))
        {
            // Fallback for tenants/apps that haven't registered the WAM broker redirect URI.
            ReconfigureWithoutBroker();

            var result = await _pca!.AcquireTokenInteractive(GraphScopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(parentWindowHandle)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return new UserSession
            {
                DisplayName = result.Account?.Username ?? "Signed-in user",
                Upn = result.Account?.Username ?? string.Empty,
                MsalAccount = result.Account!
            };
        }
    }

    public Task<string> GetGraphAccessTokenAsync(UserSession session, IntPtr parentWindowHandle)
    {
        return GetAccessTokenForScopesAsync(session, GraphScopes, parentWindowHandle);
    }

    public Task<string> GetManagementAccessTokenAsync(UserSession session, IntPtr parentWindowHandle)
    {
        return GetAccessTokenForScopesAsync(session, ManagementScopes, parentWindowHandle);
    }

    private async Task<string> GetAccessTokenForScopesAsync(UserSession session, string[] scopes, IntPtr parentWindowHandle)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        try
        {
            var silentResult = await _pca.AcquireTokenSilent(scopes, session.MsalAccount)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return silentResult.AccessToken;
        }
        catch (MsalClientException ex) when (_useBroker && IsBrokerConfigurationError(ex))
        {
            ReconfigureWithoutBroker();
            return await AcquireInteractiveWithLoginHintAsync(scopes, session, parentWindowHandle).ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            return await AcquireInteractiveForSessionAsync(scopes, session, parentWindowHandle).ConfigureAwait(false);
        }
    }

    private async Task<string> AcquireInteractiveForSessionAsync(string[] scopes, UserSession session, IntPtr parentWindowHandle)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        try
        {
            var interactiveResult = await _pca.AcquireTokenInteractive(scopes)
                .WithAccount(session.MsalAccount)
                .WithParentActivityOrWindow(parentWindowHandle)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return interactiveResult.AccessToken;
        }
        catch (MsalClientException ex) when (_useBroker && IsBrokerConfigurationError(ex))
        {
            ReconfigureWithoutBroker();
            return await AcquireInteractiveWithLoginHintAsync(scopes, session, parentWindowHandle).ConfigureAwait(false);
        }
    }

    private async Task<string> AcquireInteractiveWithLoginHintAsync(string[] scopes, UserSession session, IntPtr parentWindowHandle)
    {
        if (_pca is null)
        {
            throw new InvalidOperationException("AuthService is not configured.");
        }

        var interactiveResult = await _pca.AcquireTokenInteractive(scopes)
            .WithLoginHint(session.Upn)
            .WithPrompt(Prompt.SelectAccount)
            .WithParentActivityOrWindow(parentWindowHandle)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return interactiveResult.AccessToken;
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

    private static IPublicClientApplication BuildClient(string tenantId, string clientId, bool useBroker)
    {
        var builder = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithDefaultRedirectUri();

        if (useBroker)
        {
            builder = builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                Title = "Sign in to Azure Virtual Desktop Launcher"
            });
        }

        return builder.Build();
    }

    private void ReconfigureWithoutBroker()
    {
        if (string.IsNullOrWhiteSpace(_tenantId) || string.IsNullOrWhiteSpace(_clientId))
        {
            return;
        }

        _useBroker = false;
        _pca = BuildClient(_tenantId, _clientId, useBroker: false);
    }

    private static bool IsBrokerConfigurationError(MsalClientException ex)
    {
        return (ex.Message ?? string.Empty).IndexOf("IncorrectConfiguration", StringComparison.OrdinalIgnoreCase) >= 0
               || (ex.Message ?? string.Empty).IndexOf("ms-appx-web://microsoft.aad.brokerplugin", StringComparison.OrdinalIgnoreCase) >= 0
               || (ex.ErrorCode ?? string.Empty).IndexOf("wam", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
