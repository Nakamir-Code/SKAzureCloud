// <copyright file="WAPLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System;
using System.Threading.Tasks;

#if WINDOWS_UWP
using Windows.UI.ApplicationSettings;
#endif

public class WAPLoginProvider(IAADLogger logger, IUserStore userStore, string clientId, string tenantId) : BaseLoginProvider(logger, userStore, clientId, tenantId)
{
    public override string UserIdKey
    {
        get
        {
            return LoginHint is null ? "UserIdWAP" : $"UserIdWAP_{LoginHint}";
        }
    }

    public override string Description => $"Use the AccountsSettingsPane to connect your Universal Windows Platform (UWP) app to external identity providers";

    public override string ProviderName => $"WindowsAccountProvider";

    public override async Task<IToken> LoginAsync(string[] scopes)
    {
        Logger.Log("Login in with WindowsAccountProvider...");
        await ChooseFromAccountsAsync();
        return null;
    }

    public async Task ChooseFromAccountsAsync()
    {
#if WINDOWS_UWP
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        {
            try
            {
                //await AccountsSettingsPane.ShowAddAccountAsync();
                AccountsSettingsPane.Show();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }).AsTask();
        await tcs.Task;
#endif
    }

    public override Task SignOutAsync()
    {
        Username = string.Empty;
        AADToken = string.Empty;
        AccessToken = string.Empty;
        return Task.CompletedTask;
    }
}
