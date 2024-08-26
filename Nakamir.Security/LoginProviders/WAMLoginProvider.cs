#if WINDOWS_UWP
// <copyright file="WAMLoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nakamir.Common;
using StereoKit;
using Windows.ApplicationModel.Core;
using Windows.Security.Authentication.Web;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;

public class WAMLoginProvider(IUserStore userStore, string clientId, string tenantId, string resource, bool biometricsRequired = true) : ILoginProvider
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable biometrics (e.g., user pin or iris eye scan)
    /// before attempting to sign the user in.
    /// </summary>
    public bool BiometricsRequired { get; set; } = biometricsRequired;

    /// <summary>
    /// Gets or sets the resource for authentication.
    /// </summary>
    public string Resource { get; set; } = resource;

    /// <summary>
    /// Gets a byte array representing the authenticated user's profile picture.
    /// </summary>
    public byte[] UserPicture { get; private set; }

    /// <inheritdoc/>
    public string ProviderName => "WebAuthenticationCoreManager";

    /// <inheritdoc/>
    public string Description => "Contains core methods for obtaining tokens from web account providers.";

    /// <inheritdoc/>
    public string UserIdKey => "UserIdWAM";

    /// <inheritdoc/>
    public string AccessToken { get; private set; }

    /// <inheritdoc/>
    public string Username { get; private set; }

    /// <inheritdoc/>
    public async Task<string> LoginAsync(string[] scopes)
    {
        Log.Info("Logging in with WAM...");

        string accessToken = string.Empty;
        if (BiometricsRequired)
        {
            if (await UserConsentVerifier.CheckAvailabilityAsync() is UserConsentVerifierAvailability.Available)
            {
                UserConsentVerificationResult consentResult = await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                    () => UserConsentVerifier.RequestVerificationAsync("Please verify your credentials.").AsTask());

                if (consentResult is not UserConsentVerificationResult.Verified)
                {
                    Log.Err("Biometric verification failed.");
                    return null;
                }
            }
            else
            {
                Log.Err("Biometric verification is not available or not configured.");
                return null;
            }
        }

        string userId = userStore.GetUserId(UserIdKey);
        Log.Info("User Id: " + userId);

        string URI = string.Format("ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}",
            WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper());
        Log.Info("Redirect URI: " + URI);

        WebAccountProvider accountProvider = await WebAuthenticationCoreManager.FindAccountProviderAsync(
            "https://login.microsoft.com", $"https://login.microsoftonline.com/{tenantId}");

        Log.Info($"Found Web Account Provider for organizations: {accountProvider.DisplayName}");
        FindAllAccountsResult accountsResult = await WebAuthenticationCoreManager.FindAllAccountsAsync(accountProvider);
        Log.Info($"Find All Accounts Status = {accountsResult.Status}");

        if (accountsResult.Status is FindAllWebAccountsStatus.Success)
        {
            foreach (WebAccount acct in accountsResult.Accounts)
            {
                Log.Info($"Account: {acct.UserName} {acct.State}");
            }
        }

        WebAccountProvider systemProvider = await WebAuthenticationCoreManager.FindSystemAccountProviderAsync(accountProvider.Id);
        if (systemProvider is not null)
        {
            string displayName = "Not Found";
            if (systemProvider.User is not null)
            {
                displayName = (string)await systemProvider.User.GetPropertyAsync("DisplayName");
                Log.Info($"Found system account provider {systemProvider.DisplayName} with user {displayName} {systemProvider.User.AuthenticationStatus}");
            }
        }
        Log.Info("Web Account Provider: " + accountProvider.DisplayName);

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
            WebTokenRequestResult result = null;
            try
            {
                result = await CoreApplication.MainView.Dispatcher.RunTaskAsync(
                    () => account is not null
                        ? WebAuthenticationCoreManager.RequestTokenAsync(tokenRequest, account).AsTask()
                        : WebAuthenticationCoreManager.RequestTokenAsync(tokenRequest).AsTask());
            }
            catch (Exception ex)
            {
                Log.Err(ex.Message);
            }

            if (result is not null)
            {
                Log.Info("Interactive Token Response: " + result.ResponseStatus.ToString());
                if (result.ResponseError is not null)
                {
                    Log.Err("Error Code: " + result.ResponseError.ErrorCode.ToString());
                    Log.Err("Error Msg: " + result.ResponseError.ErrorMessage.ToString());
                    foreach (var errProp in result.ResponseError.Properties)
                    {
                        Log.Err($"Error prop: ({errProp.Key}, {errProp.Value})");
                    }
                }

                if (result.ResponseStatus is WebTokenRequestStatus.Success)
                {
                    accessToken = result.ResponseData[0].Token;
                    account = result.ResponseData[0].WebAccount;
                    var properties = result.ResponseData[0].Properties;
                    Username = account.UserName;
                    Log.Info($"Username = {Username}");
                    var ras = await account.GetPictureAsync(WebAccountPictureSize.Size64x64);
                    Stream stream = ras.AsStreamForRead();
                    BinaryReader reader = new(stream);
                    UserPicture = reader.ReadBytes((int)stream.Length);
                    Log.Info("Access Token: " + accessToken);
                }
            }
        }

        if (tokenResponse.ResponseStatus is WebTokenRequestStatus.Success)
        {
            foreach (var response in tokenResponse.ResponseData)
            {
                string name = response.WebAccount.UserName;
                accessToken = response.Token;
                account = response.WebAccount;
                Username = account.UserName;
                Log.Info($"Username = {Username}");
                try
                {
                    var ras = await account.GetPictureAsync(WebAccountPictureSize.Size64x64);
                    var stream = ras.AsStreamForRead();
                    var br = new BinaryReader(stream);
                    UserPicture = br.ReadBytes((int)stream.Length);
                }
                catch (Exception ex)
                {
                    Log.Err($"Exception when reading image {ex.Message}");
                }
            }
            Log.Info("Access Token: " + accessToken);
        }

        if (account != null && !string.IsNullOrEmpty(account.Id))
        {
            userStore.SaveUser(UserIdKey, account.Id);
        }

        AccessToken = accessToken;
        return AccessToken;
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
