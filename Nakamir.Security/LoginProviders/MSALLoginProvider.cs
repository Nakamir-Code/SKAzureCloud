// <copyright file="MSALLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security.LoginProviders;

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using StereoKit;

#if WINDOWS_UWP
using Nakamir.Common;
using Windows.ApplicationModel.Core;
using Windows.System;
#endif

using LogLevel = Microsoft.Identity.Client.LogLevel;

public class MSALLoginProvider(IUserStore userStore, string clientId, string tenantId, string? redirectUri = null, Func<object>? parentActivityOrWindow = null, bool useDeviceCodeFlow = false) : ILoginProvider
{
    /// <summary>
    /// Gets or sets a value indicating whether to provide a code for the user to sign in instead of their credentials.
    /// </summary>
    public bool UseDeviceCodeFlow { get; set; } = useDeviceCodeFlow;

    /// <summary>
    /// Gets or sets a value indicating whether the interactive authentication should be an embedded or system view.
    /// </summary>
    public bool UseEmbedded { get; set; }

    /// <summary>
    /// Gets or sets a username (i.e., email address) of the account to prevent manual typing.
    /// </summary>
    public string? LoginHint { get; set; }

    /// <inheritdoc/>
    public string ProviderName => "Microsoft Authentication Library (MSAL)";

    /// <inheritdoc/>
    public string Description => "Microsoft Authentication Library (MSAL) enables developers to acquire tokens from the Microsoft identity platform endpoint in order to access secured web APIs. These web APIs can be the Microsoft Graph, other Microsoft APIs, third-party web APIs, or your own web API. MSAL is available for .NET, JavaScript, Android, and iOS, which support many different application architectures and platforms.";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdMSAL";

    /// <inheritdoc/>
    public string AccessToken { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public string Username { get; private set; } = string.Empty;

    private IPublicClientApplication? _publicClientApplication;

    /// <inheritdoc/>
    public async Task<string?> LoginAsync(string[]? scopes = null)
    {
        Log.Info("Logging in with MSAL...");
        string url = $"https://login.microsoftonline.com/{tenantId}";

        _publicClientApplication ??= PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(url)
#if ANDROID || IOS
			.WithBroker()
#elif WINDOWS
			.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                ListOperatingSystemAccounts = true,
            })
#elif WINDOWS_UWP
            .WithBroker()
            .WithWindowsBrokerOptions(new WindowsBrokerOptions
            {
                ListWindowsWorkAndSchoolAccounts = true,
            })
#endif
            .WithRedirectUri(redirectUri ?? "https://login.microsoftonline.com/common/oauth2/nativeclient")
            .WithParentActivityOrWindow(parentActivityOrWindow)
            .WithClientCapabilities(["cp1"]) // declare this client app capable of receiving CAE events: https://aka.ms/clientcae
            .WithIosKeychainSecurityGroup("com.microsoft.adalcache")
            .WithLogging(LoggingCallback)
            .Build();

        IAccount? account = await FindAccountAsync().ConfigureAwait(false);
        AuthenticationResult? authResult = null;
        try
        {
            if (account is not null)
            {
                authResult = await _publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync().ConfigureAwait(false);
            }
            else
            {
                if (!UseDeviceCodeFlow && _publicClientApplication.IsUserInteractive())
                {
                    if (UseEmbedded)
                    {
#if WINDOWS_UWP
                        authResult = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(() => _publicClientApplication
#else
                        authResult = await _publicClientApplication
#endif
                            .AcquireTokenInteractive(scopes)
                            .WithLoginHint(LoginHint ?? string.Empty)
                            .WithUseEmbeddedWebView(true)
                            .WithParentActivityOrWindow(parentActivityOrWindow)
                            .ExecuteAsync()
#if WINDOWS_UWP
                            );
#else
                            .ConfigureAwait(false);
#endif
                    }
                    else
                    {
                        SystemWebViewOptions systemWebViewOptions = new()
                        {
#if IOS
							// Hide the privacy prompt in iOS
							iOSHidePrivacyPrompt = true;
#endif
                        };

#if WINDOWS_UWP
                        authResult = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(() => _publicClientApplication
#else
                        authResult = await _publicClientApplication
#endif
                            .AcquireTokenInteractive(scopes)
                            .WithLoginHint(LoginHint ?? string.Empty)
                            .WithSystemWebViewOptions(systemWebViewOptions)
                            .WithParentActivityOrWindow(parentActivityOrWindow)
                            .ExecuteAsync()
#if WINDOWS_UWP
                            );
#else
                            .ConfigureAwait(false);
#endif
                    }
                }
            }

            // If the operating system does not have UI (e.g. SSH into Linux), you can fallback to device code, however this
            // flow will not satisfy the "device is managed" CA policy.
            authResult ??= await _publicClientApplication.AcquireTokenWithDeviceCode(scopes, async deviceCodeResult =>
            {
                Log.Info(deviceCodeResult.Message);
#if WINDOWS_UWP
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                    () => Launcher.LaunchUriAsync(new("https://microsoft.com/devicelogin")).AsTask());
#else
				await Task.CompletedTask;
#endif
            }).ExecuteAsync().ConfigureAwait(false);
        }
        catch (MsalUiRequiredException msalUiEx)
        {
            try
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenInteractive to acquire a token interactively
                Log.Warn($"MsalUiRequiredException: {msalUiEx.Message}");

#if WINDOWS_UWP
                authResult = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(() => _publicClientApplication
#else
                authResult = await _publicClientApplication
#endif
                    .AcquireTokenInteractive(scopes)
                    .WithLoginHint(LoginHint ?? string.Empty)
                    .WithParentActivityOrWindow(parentActivityOrWindow)
                    .ExecuteAsync()
#if WINDOWS_UWP
                    );
#else
                    .ConfigureAwait(false);
#endif
            }
            catch (Exception ex)
            {
                Log.Err(ex.Message);
            }
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
            IAccount? account = await FindAccountAsync().ConfigureAwait(false);
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

    private async Task<IAccount?> FindAccountAsync()
    {
        string userId = userStore.GetUserId(UserIdKey);
        Log.Info("User Id: " + userId);

        IAccount? account = null;
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

    private void LoggingCallback(LogLevel level, string message, bool containsPii)
    {
        switch (level)
        {
            case LogLevel.Error: Log.Err(message); break;
            case LogLevel.Warning: Log.Warn(message); break;
            case LogLevel.Info: Log.Info(message); break;
            case LogLevel.Verbose:
            case LogLevel.Always:
            default: Log.Write(StereoKit.LogLevel.Diagnostic, message); break;
        }
    }
}
