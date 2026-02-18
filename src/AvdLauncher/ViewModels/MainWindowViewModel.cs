using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using AvdLauncher.Models;
using AvdLauncher.Services;

namespace AvdLauncher.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AuthService _authService = new();
    private readonly GraphService _graphService = new();
    private readonly AvdService _avdService = new();
    private readonly WindowsAppLauncher _launcher = new();

    private UserSession? _selectedUser;
    private string _tenantId = "<tenant-id>";
    private string _clientId = "<client-id>";
    private string _subscriptionId = "<subscription-id>";
    private string _resourceGroup = "<resource-group>";
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public MainWindowViewModel()
    {
        AddUserCommand = new RelayCommand(async _ => await AddUserAsync(), _ => CanRunActions());
        RefreshAppsCommand = new RelayCommand(async _ => await RefreshAppsAsync(), _ => CanRunActions() && SelectedUser is not null);
        LaunchAppCommand = new RelayCommand(app => LaunchApp((RemoteAppInfo)app!), _ => true);
    }

    public ObservableCollection<UserSession> Users { get; } = new();
    public ObservableCollection<RemoteAppInfo> RemoteApps { get; } = new();

    public RelayCommand AddUserCommand { get; }
    public RelayCommand RefreshAppsCommand { get; }
    public RelayCommand LaunchAppCommand { get; }

    public string TenantId
    {
        get => _tenantId;
        set => SetField(ref _tenantId, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetField(ref _clientId, value);
    }

    public string SubscriptionId
    {
        get => _subscriptionId;
        set => SetField(ref _subscriptionId, value);
    }

    public string ResourceGroup
    {
        get => _resourceGroup;
        set => SetField(ref _resourceGroup, value);
    }

    public UserSession? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetField(ref _selectedUser, value))
            {
                RefreshAppsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool CanRunActions() => !_isBusy;

    private async Task AddUserAsync()
    {
        try
        {
            _isBusy = true;
            RaiseCanExecutes();

            _authService.Configure(TenantId.Trim(), ClientId.Trim());

            StatusMessage = "Opening Microsoft sign-in popup...";
            var user = await _authService.AddUserInteractiveAsync();
            var token = await _authService.GetAccessTokenAsync(user);
            var displayName = await _graphService.GetDisplayNameAsync(token);

            var enrichedUser = new UserSession
            {
                DisplayName = displayName,
                Upn = user.Upn,
                MsalAccount = user.MsalAccount
            };

            Users.Add(enrichedUser);
            SelectedUser = enrichedUser;
            StatusMessage = $"Added {displayName}. Launches should use Windows account SSO (no extra popup when Windows App is already signed in).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add user failed: {ex.Message}";
            MessageBox.Show(StatusMessage, "Authentication error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
            RaiseCanExecutes();
        }
    }

    private async Task RefreshAppsAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            _isBusy = true;
            RaiseCanExecutes();

            StatusMessage = "Loading AVD workspace/app-group apps...";
            var accessToken = await _authService.GetAccessTokenAsync(SelectedUser);
            var apps = await _avdService.GetAssignedRemoteAppsAsync(accessToken, SubscriptionId.Trim(), ResourceGroup.Trim());

            RemoteApps.Clear();
            foreach (var app in apps)
            {
                RemoteApps.Add(app);
            }

            StatusMessage = $"Loaded {RemoteApps.Count} apps for {SelectedUser.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            MessageBox.Show(StatusMessage, "AVD fetch error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
            RaiseCanExecutes();
        }
    }

    private void LaunchApp(RemoteAppInfo app)
    {
        try
        {
            var launchUri = app.LaunchUri ?? string.Empty;
            _launcher.Launch(launchUri);
            StatusMessage = $"Sent launch request for {app.Name}. Windows App should reuse WAM session to avoid another login prompt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Launch failed: {ex.Message}";
            MessageBox.Show(StatusMessage, "Launch error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void RaiseCanExecutes()
    {
        AddUserCommand.RaiseCanExecuteChanged();
        RefreshAppsCommand.RaiseCanExecuteChanged();
    }
}
