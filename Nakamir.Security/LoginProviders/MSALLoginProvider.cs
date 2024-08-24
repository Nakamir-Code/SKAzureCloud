// <copyright file="MSALLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security.LoginProviders;

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

public class MSALLoginProvider(IAADLogger Logger, IUserStore store, string clientId, string tenantId) : BaseLoginProvider(Logger, store, clientId, tenantId)
{
    public bool UseDeviceCodeFlow { get; set; } = false;

    public override string UserIdKey
    {
        get
        {
            return LoginHint is null ? "UserIdMSAL" : $"UserIdMSAL_{LoginHint}";
        }
    }

    public string TenantId { get; } = tenantId;

    public override string Description => $"Microsoft Authentication Library (MSAL) enables developers to acquire tokens from the Microsoft identity platform endpoint in order to access secured web APIs. These web APIs can be the Microsoft Graph, other Microsoft APIs, third-party web APIs, or your own web API. MSAL is available for .NET, JavaScript, Android, and iOS, which support many different application architectures and platforms.";

    public override string ProviderName => $"Microsoft Authentication Library (MSAL)";

    public override async Task<IToken> LoginAsync(string[] scopes)
    {
        Logger.Log("Logging in with MSAL...");
        string url = $"https://login.microsoftonline.com/{TenantId}";

        string userId = Store.GetUserId(UserIdKey);
        Logger.Log("User Id: " + userId);

        var ret = await Task.Run(async () =>
        {
            var app = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(url)
                .WithBroker()
                .WithWindowsBrokerOptions(new WindowsBrokerOptions
                {
                    ListWindowsWorkAndSchoolAccounts = true,
                })
                .WithRedirectUri(NativeClientRedirectUri)
                .WithLogging(LoggingCallback)
                .Build();

            IAccount account = null;
            if (!string.IsNullOrEmpty(userId))
            {
                account = await app.GetAccountAsync(userId);
                if (account != null)
                    Logger.Log($"Account found id = {account.HomeAccountId}");
            }

            AuthenticationResult authResult = null;

            try
            {
                authResult = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    if (!UseDeviceCodeFlow)
                    {
#if WINDOWS_UWP
                        TaskCompletionSource<AuthenticationResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                        {
                            try
                            {
                                tcs.SetResult(await app.AcquireTokenInteractive(scopes)
                                    .WithLoginHint(LoginHint ?? string.Empty)
                                    .ExecuteAsync());
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                            }
                        }).AsTask();
                        authResult = await tcs.Task;
#else
                        authResult = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
#endif
                    }
                    else
                    {
                        authResult = await app.AcquireTokenWithDeviceCode(scopes, async deviceCodeResult =>
                        {
                            Logger.Log(deviceCodeResult.Message);
                            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                                () => Windows.System.Launcher.LaunchUriAsync(new("https://microsoft.com/devicelogin")).AsTask().ConfigureAwait(false));
                            //return Task.FromResult(0);
                        })
                        .ExecuteAsync();
                    }
                }
                catch (MsalException msalEx)
                {
                    Logger.Log(msalEx.Message);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message);
                }
            }
            catch (MsalException msalEx)
            {
                Logger.Log(msalEx.Message);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
            }

            if (authResult == null)
                return null;

            Username = authResult.Account.Username;

            Logger.Log("Acquired Access Token");
            Logger.Log("Access Token: " + authResult.AccessToken, true);

            return authResult;
        });

        if (ret is null)
        {
            return null;
        }

        if (ret.Account != null && !string.IsNullOrEmpty(ret.Account.HomeAccountId.Identifier))
        {
            Store.SaveUser(UserIdKey, ret.Account.HomeAccountId.Identifier);
        }

        AADToken = ret.AccessToken;
        return new AADToken(ret.AccessToken);
    }

    public override Task SignOutAsync()
    {
        Logger.Clear();
        Store.ClearUser(UserIdKey);
        AADToken = string.Empty;
        AccessToken = string.Empty;
        Username = string.Empty;
        return Task.CompletedTask;
    }

    private void LoggingCallback(LogLevel level, string message, bool containsPii)
    {
        Logger.Log(message, true);
    }
}
