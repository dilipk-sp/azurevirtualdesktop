using System;
using System.Diagnostics;

namespace AvdLauncher.Services;

public sealed class WindowsAppLauncher
{
    public void Launch(string launchUri)
    {
        if (string.IsNullOrWhiteSpace(launchUri))
        {
            throw new ArgumentException("A launch URI is required.", nameof(launchUri));
        }

        Process.Start(new ProcessStartInfo(launchUri)
        {
            UseShellExecute = true
        });
    }
}
