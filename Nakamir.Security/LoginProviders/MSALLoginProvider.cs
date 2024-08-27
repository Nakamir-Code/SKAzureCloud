// <copyright file="MSALLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security.LoginProviders;

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using StereoKit;

#if WINDOWS_UWP
using Nakamir.Common;
using Windows.ApplicationModel.Core;
using Windows.System;
#endif

public class MSALLoginProvider(IUserStore userStore, string clientId, string tenantId, bool useDeviceCodeFlow = false) : ILoginProvider
{
    /// <summary>
    /// Gets or sets a value indicating whether to provide a code for the user to sign in instead of their credentials.
    /// </summary>
    public bool UseDeviceCodeFlow { get; set; } = useDeviceCodeFlow;

    /// <summary>
    /// Gets or sets a username (i.e., email address) of the account to prevent manual typing.
    /// </summary>
    public string LoginHint { get; set; }

    /// <inheritdoc/>
    public string ProviderName => "Microsoft Authentication Library (MSAL)";

    /// <inheritdoc/>
    public string Description => "Microsoft Authentication Library (MSAL) enables developers to acquire tokens from the Microsoft identity platform endpoint in order to access secured web APIs. These web APIs can be the Microsoft Graph, other Microsoft APIs, third-party web APIs, or your own web API. MSAL is available for .NET, JavaScript, Android, and iOS, which support many different application architectures and platforms.";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdMSAL";

    /// <inheritdoc/>
    public string AccessToken { get; private set; }

    /// <inheritdoc/>
    public string Username { get; private set; }

    private IPublicClientApplication _publicClientApplication;

    /// <inheritdoc/>
    public async Task<string> LoginAsync(string[] scopes)
    {
        Log.Info("Logging in with MSAL...");
        string url = $"https://login.microsoftonline.com/{tenantId}";

        _publicClientApplication ??= PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(url)
            .WithBroker()
            .WithWindowsBrokerOptions(new WindowsBrokerOptions
            {
                ListWindowsWorkAndSchoolAccounts = true,
            })
            .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
            .WithLogging(LoggingCallback)
            .Build();

        IAccount account = await FindAccountAsync().ConfigureAwait(false);

        AuthenticationResult authResult = null;
        try
        {
            authResult = await _publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            try
            {
                if (!UseDeviceCodeFlow)
                {
#if WINDOWS_UWP
                    authResult = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                        () => _publicClientApplication.AcquireTokenInteractive(scopes)
                                 .WithLoginHint(LoginHint ?? string.Empty)
                                 .ExecuteAsync());
#else
                        authResult = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
#endif
                }
                else
                {
                    authResult = await _publicClientApplication.AcquireTokenWithDeviceCode(scopes, async deviceCodeResult =>
                    {
                        Log.Info(deviceCodeResult.Message);
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                            () => Launcher.LaunchUriAsync(new("https://microsoft.com/devicelogin")).AsTask());
                    })
                    .ExecuteAsync();
                }
            }
            catch (MsalException msalEx)
            {
                Log.Err(msalEx.Message);
            }
            catch (Exception ex)
            {
                Log.Err(ex.Message);
            }
        }
        catch (MsalException msalEx)
        {
            Log.Err(msalEx.Message);
        }
        catch (Exception ex)
        {
            Log.Err(ex.Message);
        }

        if (authResult is null)
        {
            return null;
        }

        Username = authResult.Account.Username;

        Log.Info("Acquired Access Token");
        Log.Info("Access Token: " + authResult.AccessToken);

        if (authResult.Account is not null && !string.IsNullOrEmpty(authResult.Account.HomeAccountId.Identifier))
        {
            userStore.SaveUser(UserIdKey, authResult.Account.HomeAccountId.Identifier);
        }

        AccessToken = authResult.AccessToken;
        return AccessToken;
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        if (_publicClientApplication is not null)
        {
            IAccount account = await FindAccountAsync().ConfigureAwait(false);
            if (account is not null)
            {
                await _publicClientApplication.RemoveAsync(account).ConfigureAwait(false);
            }
        }
        userStore.ClearUser(UserIdKey);
        AccessToken = string.Empty;
        Username = string.Empty;
    }

    /// <inheritdoc/>
    public void ClearUser() => userStore.ClearUser(UserIdKey);

    private async Task<IAccount> FindAccountAsync()
    {
        string userId = userStore.GetUserId(UserIdKey);
        Log.Info("User Id: " + userId);

        IAccount account = null;
        if (!string.IsNullOrEmpty(userId))
        {
            account = await _publicClientApplication!.GetAccountAsync(userId).ConfigureAwait(false) ?? PublicClientApplication.OperatingSystemAccount;
            if (account is not null)
            {
                Log.Info($"Account found id = {account.HomeAccountId}");
                LoginHint = account.Username;
            }
        }
        return account;
    }

    private void LoggingCallback(Microsoft.Identity.Client.LogLevel level, string message, bool containsPii)
    {
        switch (level)
        {
            case Microsoft.Identity.Client.LogLevel.Error: Log.Err(message); break;
            case Microsoft.Identity.Client.LogLevel.Warning: Log.Warn(message); break;
            case Microsoft.Identity.Client.LogLevel.Info: Log.Info(message); break;
            default: Log.Write(StereoKit.LogLevel.Diagnostic, message); break;
        }
    }
}
