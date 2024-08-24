// <copyright file="WABLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System;
using System.Linq;
using System.Threading.Tasks;
#if WINDOWS_UWP
using Windows.Security.Authentication.Web;
#endif

public class WABLoginProvider(IAADLogger Logger, IUserStore userStore, string clientId, string tenantId) : BaseLoginProvider(Logger, userStore, clientId, tenantId)
{
    public override string UserIdKey
    {
        get
        {
            return LoginHint is null ? "UserIdWAB" : $"UserIdWAB_{LoginHint}";
        }
    }

    public override string Description => $"WebAuthenticationBroker facilitates acquisition of auth tokens using OAuth by handling the presentation and redirects of an identity provider page and returning the tokens back to your app.";

    public override string ProviderName => $"WebAuthenticationBroker";

    public async override Task<IToken> LoginAsync(string[] scopes)
    {
        string accessToken = string.Empty;
        Logger.Log("Logging in with WebAuthenticationBroker...");

#if WINDOWS_UWP
        var redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri;
        //var redirectUri = NativeClientRedirectUri;

        var state = Guid.NewGuid().ToString();
        var nonce = Guid.NewGuid().ToString();

        string url = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        string scope = string.Join(' ', scopes.Select(Uri.EscapeDataString));

        var uri = new Uri($"{url}?" +
            $"client_id={ClientId}&" +
            $"scope={scope} openid&" +
            $"response_type=token&" +
            $"state={Uri.EscapeDataString(state)}&" +
            $"nonce={Uri.EscapeDataString(nonce)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        //+ $"prompt=select_account"); 

        bool useEnterpriseAuth = true;
        var options = useEnterpriseAuth == true ? WebAuthenticationOptions.UseCorporateNetwork : WebAuthenticationOptions.None;

        Logger.Log("Using Start URI: ");
        Logger.Log(uri.AbsoluteUri);

        Logger.Log("Waiting for authentication...");
        try
        {
            WebAuthenticationResult result;

            Logger.Log("Trying silent auth");

            TaskCompletionSource<WebAuthenticationResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    tcs.SetResult(await WebAuthenticationBroker.AuthenticateSilentlyAsync(uri));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }).AsTask();
            result = await tcs.Task;
            Logger.Log($"Silent Auth result: {result.ResponseStatus}");

            if (result.ResponseStatus != WebAuthenticationStatus.Success)
            {
                Logger.Log($"{result.ResponseData} : [{result.ResponseErrorDetail}]");
                Logger.Log("Trying UI auth");

                tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        var res = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri, new Uri(redirectUri));
                        tcs.SetResult(res);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }).AsTask();
                result = await tcs.Task;
            }

            Logger.Log("Waiting for authentication complete.");

            switch (result.ResponseStatus)
            {
                case WebAuthenticationStatus.Success:
                    Logger.Log("Authentication Successful!");
                    Logger.Log("Received data:");
                    Logger.Log(result.ResponseData);
                    accessToken = result.ResponseData.Split('=')[1];
                    break;
                case WebAuthenticationStatus.UserCancel:
                    Logger.Log("User cancelled authentication. Try again.");
                    break;
                case WebAuthenticationStatus.ErrorHttp:
                    Logger.Log("HTTP Error. Try again.");
                    Logger.Log(result.ResponseErrorDetail.ToString());
                    break;
                default:
                    Logger.Log("Unknown Response");
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Log($"Unhandled {e} - {e.Message}");
        }
#endif

        AADToken = accessToken;
        return new AADToken(accessToken);
    }

    public override async Task SignOutAsync()
    {
        Logger.Clear();
        Logger.Log("Sign out initiated...");
#if WINDOWS_UWP
        var redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri;
        var state = Guid.NewGuid().ToString();
        var logoutUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/logout";

        var uri = new Uri($"{logoutUrl}?" +
            $"state={Uri.EscapeDataString(state)}");

        Logger.Log($"Using sign out URI: {uri.AbsoluteUri}");

        Logger.Log($"Waiting for sign out...");
        var result = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri);
#endif
        Username = string.Empty;
        AADToken = string.Empty;
        AccessToken = string.Empty;
    }
}
