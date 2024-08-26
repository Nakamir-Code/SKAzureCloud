#if WINDOWS_UWP
// <copyright file="WABLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Security.Authentication.Web;
using Nakamir.Common;
using StereoKit;

public class WABLoginProvider(IUserStore userStore, string clientId, string tenantId) : ILoginProvider
{
    /// <inheritdoc/>
    public string ProviderName => "WebAuthenticationBroker";

    /// <inheritdoc/>
    public string Description => "WebAuthenticationBroker facilitates acquisition of authorization tokens using OAuth by handling the presentation and redirects of an identity provider page and returning the tokens back to your app.";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdWAB";

    /// <inheritdoc/>
    public string AccessToken { get; private set; }

    /// <inheritdoc/>
    public string Username { get; private set; }

    /// <inheritdoc/>
    public async Task<string> LoginAsync(string[] scopes)
    {
        Log.Info("Logging in with WAB...");

        string redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri;

        string state = Guid.NewGuid().ToString();
        string nonce = Guid.NewGuid().ToString();

        string url = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        string scope = string.Join(' ', scopes.Select(Uri.EscapeDataString));

        Uri uri = new($"{url}?" +
            $"client_id={clientId}&" +
            $"tenant_id={tenantId}&" + // TODO: needed?
            $"scope={scope} openid&" +
            $"response_type=token&" +
            $"state={Uri.EscapeDataString(state)}&" +
            $"nonce={Uri.EscapeDataString(nonce)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        //+ $"prompt=select_account"); 

        bool useEnterpriseAuth = false;
        var options = useEnterpriseAuth ? WebAuthenticationOptions.UseCorporateNetwork : WebAuthenticationOptions.None;

        Log.Info("Using Start URI: ");
        Log.Info(uri.AbsoluteUri);

        try
        {
            Log.Info("Trying silent authentication");

            WebAuthenticationResult result = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                () => WebAuthenticationBroker.AuthenticateSilentlyAsync(uri).AsTask());

            Log.Info($"Silent authentication result: {result.ResponseStatus}");

            if (result.ResponseStatus is not WebAuthenticationStatus.Success)
            {
                Log.Err($"{result.ResponseData} : [{result.ResponseErrorDetail}]");
                Log.Info("Trying interactive authentication");
                result = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                    () => WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri, new Uri(redirectUri)).AsTask());
            }

            switch (result.ResponseStatus)
            {
                case WebAuthenticationStatus.Success:
                    Log.Info("Authentication Successful!");
                    Log.Info("Received data:");
                    Log.Info(result.ResponseData);
                    AccessToken = result.ResponseData.Split('=')[1];
                    break;
                case WebAuthenticationStatus.UserCancel:
                    Log.Info("User cancelled authentication. Try again.");
                    break;
                case WebAuthenticationStatus.ErrorHttp:
                    Log.Err("HTTP Error. Try again.");
                    Log.Err(result.ResponseErrorDetail.ToString());
                    break;
                default:
                    Log.Err("Unknown Response");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Err($"Unhandled {ex} - {ex.Message}");
        }

        return AccessToken;
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        //string redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri;
        string state = Guid.NewGuid().ToString();
        string logoutUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/logout";

        Uri uri = new($"{logoutUrl}?state={Uri.EscapeDataString(state)}");
        Log.Info($"Using sign out URI: {uri.AbsoluteUri}");

        await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri);
        AccessToken = string.Empty;
        Username = string.Empty;
    }

    /// <inheritdoc/>
    public void ClearUser() => userStore.ClearUser(UserIdKey);
}
#endif
