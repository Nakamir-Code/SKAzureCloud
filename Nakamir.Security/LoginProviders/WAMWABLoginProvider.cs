#if WINDOWS_UWP
// <copyright file="WAMWABLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System;
using System.Linq;
using System.Threading.Tasks;
using Nakamir.Common;
using StereoKit;
using Windows.ApplicationModel.Core;
using Windows.Security.Authentication.Web;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;

public class WAMWABLoginProvider(IUserStore userStore, string clientId, string tenantId, string resource) : ILoginProvider
{
    /// <summary>
    /// Gets or sets the resource for authentication.
    /// </summary>
    public string Resource { get; set; } = resource;

    /// <inheritdoc/>
    public string ProviderName => $"WebAuthenticationBroker & WebAuthenticationCoreManager";

    /// <inheritdoc/>
    public string Description => "Contains methods to log in with a combination of WebAuthenticationBroker and WebAuthenticationCoreManager.";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdWAMWAB";

    /// <inheritdoc/>
    public string AccessToken { get; private set; }

    /// <inheritdoc/>
    public string Username { get; private set; }

    /// <inheritdoc/>
    public async Task<string> LoginAsync(string[] scopes)
    {
        Log.Info("Logging in with a combination of WAM and WAB...");

        string accessToken = string.Empty;

        string userId = userStore.GetUserId(UserIdKey);
        Log.Info("User Id: " + userId);

        //string URI = string.Format("ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}", 
        //    WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper());
        WebAccountProvider accountProvider = await WebAuthenticationCoreManager.FindAccountProviderAsync(
            "https://login.microsoft.com", $"https://login.microsoftonline.com/{tenantId}");

        Log.Info($"Found Web Account Provider for organizations: {accountProvider.DisplayName}");

        var accts = await WebAuthenticationCoreManager.FindAllAccountsAsync(accountProvider);

        Log.Info($"Find All Accounts Status = {accts.Status}");

        if (accts.Status is FindAllWebAccountsStatus.Success)
        {
            foreach (var acct in accts.Accounts)
            {
                Log.Info($"Account: {acct.UserName} {acct.State.ToString()}");
            }
        }

        var sap = await WebAuthenticationCoreManager.FindSystemAccountProviderAsync(accountProvider.Id);
        if (sap is not null)
        {
            string displayName = "Not Found";
            if (sap.User is not null)
            {
                displayName = (string)await sap.User.GetPropertyAsync("DisplayName");
                Log.Info($"Found system account provider {sap.DisplayName} with user {displayName} {sap.User.AuthenticationStatus}");
            }
        }

        Log.Info("Web Account Provider: " + accountProvider.DisplayName);

        //string resource = "https://sts.mixedreality.azure.com";
        //var scope = "https://management.azure.com/user_impersonation";
        //WebTokenRequest wtr = new WebTokenRequest(accountProvider, scope, "3c663152-fdf9-4033-963f-c398c21212d9");
        //WebTokenRequest wtr = new WebTokenRequest(accountProvider, scope, "5c8c830a-4cf8-470e-ba0d-6d815feba800");

        string scope = string.Join(' ', scopes.Select(Uri.EscapeDataString));
        WebTokenRequest tokenRequest = new(accountProvider, scope, clientId);
        tokenRequest.Properties.Add("resource", Resource);

        WebAccount account = null;
        if (!string.IsNullOrEmpty(userId))
        {
            account = await WebAuthenticationCoreManager.FindAccountAsync(accountProvider, userId);
            if (account is not null)
            {
                Log.Info("Found account: " + account.UserName);
            }
            else
            {
                Log.Info("Account not found");
            }
        }

        WebTokenRequestResult tokenResponse = null;
        try
        {
            tokenResponse = account is not null
                ? await WebAuthenticationCoreManager.GetTokenSilentlyAsync(tokenRequest, account)
                : await WebAuthenticationCoreManager.GetTokenSilentlyAsync(tokenRequest);
        }
        catch (Exception ex)
        {
            Log.Err(ex.Message);
        }

        Log.Info("Silent Token Response: " + tokenResponse.ResponseStatus.ToString());
        if (tokenResponse.ResponseError is not null)
        {
            Log.Err("Error Code: " + tokenResponse.ResponseError.ErrorCode.ToString());
            Log.Err("Error Msg: " + tokenResponse.ResponseError.ErrorMessage.ToString());
            foreach (var errProp in tokenResponse.ResponseError.Properties)
            {
                Log.Err($"Error prop: ({errProp.Key}, {errProp.Value})");
            }
        }

        if (tokenResponse.ResponseStatus is WebTokenRequestStatus.UserInteractionRequired)
        {
            string redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri;

            string state = Guid.NewGuid().ToString();
            string nonce = Guid.NewGuid().ToString();

            //string url = "https://login.microsoftonline.com/common";
            string url = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

            var uri = new Uri($"{url}?" +
                $"client_id={clientId}&" +
                $"tenant_id={tenantId}&" + // TODO: needed?
                //$"scope={scope} openid&" +
                $"response_type=token&" +
                $"state={Uri.EscapeDataString(state)}&" +
                $"nonce={Uri.EscapeDataString(nonce)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}");

            var result = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                () => WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri, new Uri(redirectUri)).AsTask());

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

            if (account is not null && !string.IsNullOrEmpty(account.Id))
            {
                userStore.SaveUser(UserIdKey, account.Id);
            }
        }

        return accessToken;
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        string userId = userStore.GetUserId(UserIdKey);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        WebAccountProvider wap = await WebAuthenticationCoreManager.FindAccountProviderAsync(
            "https://login.microsoft.com", $"https://login.microsoftonline.com/{tenantId}");
        WebAccount account = await WebAuthenticationCoreManager.FindAccountAsync(wap, userId);
        Log.Info($"Found account: {account.UserName} State: {account.State}");
        await account.SignOutAsync();
        userStore.ClearUser(UserIdKey);
        AccessToken = string.Empty;
        Username = string.Empty;
    }

    /// <inheritdoc/>
    public void ClearUser() => userStore.ClearUser(UserIdKey);

}
#endif
