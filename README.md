# Azure Virtual Desktop WPF Launcher (.NET Framework 4.8)

This repository contains a WPF desktop application targeting **.NET Framework 4.8** that lets you:

1. Add multiple Entra users using an **MSAL interactive popup**.
2. Acquire tokens for Microsoft Graph + Azure management APIs.
3. Read Azure Virtual Desktop (AVD) app groups/workspaces/apps.
4. Show remote apps in a grid with a launch button.
5. Launch Windows App via URI protocol so the local Windows account/session can provide SSO.

## Project structure

- `AvdLauncher.sln`
- `src/AvdLauncher/AvdLauncher.csproj`
- `src/AvdLauncher/MainWindow.xaml`
- `src/AvdLauncher/ViewModels/MainWindowViewModel.cs`
- `src/AvdLauncher/Services/AuthService.cs`
- `src/AvdLauncher/Services/AvdService.cs`

## Prerequisites

- Windows 10/11 machine with Visual Studio 2022.
- .NET Framework 4.8 developer pack.
- A registered Entra application (public client).
- Windows App installed: https://learn.microsoft.com/en-us/windows-app/landing

## Entra App registration settings

Configure the client app as a **public client/native app**:

- Redirect URI: default MSAL desktop redirect URI.
- Allow public client flows: enabled.
- API permissions:
  - `User.Read` (Microsoft Graph, delegated)
  - `user_impersonation` for Azure Service Management

For AVD data access, user(s) signing in must have Azure RBAC permissions for Desktop Virtualization resources.

## How no second login prompt is achieved

- The app authenticates through MSAL with **Broker/WAM**.
- Windows App also uses the same Windows account broker/session.
- When both use the same Entra account and tenant, launch should reuse broker SSO and avoid a second popup.

> If users still see extra prompts, verify account alignment in Windows, app registration authority, conditional access policies, and whether Windows App already has the same account added.

## Notes

- `AvdService` currently reads AVD resources from ARM endpoints in a subscription/resource-group scope and builds an `ms-rd:subscribe` URI for launch subscription.
- Depending on your tenant design, you may extend it to fetch per-user assignments more strictly (for example, by checking assignments on application groups).
