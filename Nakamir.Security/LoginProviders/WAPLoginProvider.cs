#if WINDOWS_UWP
// <copyright file="WAPLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System.Threading.Tasks;
using Nakamir.Common;
using StereoKit;
using Windows.ApplicationModel.Core;
using Windows.UI.ApplicationSettings;

public class WAPLoginProvider(IUserStore userStore/*, string clientId, string tenantId*/) : ILoginProvider
{
    /// <inheritdoc/>
    public string ProviderName => "WindowsAccountProvider";

    /// <inheritdoc/>
    public string Description => "Use the AccountsSettingsPane to connect your Universal Windows Platform (UWP) app to external identity providers";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdWAP";

    /// <inheritdoc/>
    public string AccessToken { get; private set; }

    /// <inheritdoc/>
    public string Username { get; private set; }

    /// <inheritdoc/>
    public async Task<string> LoginAsync(string[] scopes)
    {
        Log.Info("Logging in with WAP...");
        await ChooseFromAccountsAsync();
        return null;
    }

    /// <inheritdoc/>
    public Task LogoutAsync()
    {
        Username = string.Empty;
        AccessToken = string.Empty;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ClearUser() => userStore.ClearUser(UserIdKey);

    public Task ChooseFromAccountsAsync()
    {
        return CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
            () =>
            {
                //await AccountsSettingsPane.ShowAddAccountAsync();
                AccountsSettingsPane.Show();
                return Task.CompletedTask;
            });
    }
}
#endif
