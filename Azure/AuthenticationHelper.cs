// <copyright file="AuthenticationHelper.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Azure.Security;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using StereoKit;
#if WINDOWS_UWP
using Windows.Security.Authentication.Web;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
#endif

/// <summary>
/// Authentication helper methods.
/// </summary>
public static class AuthenticationHelper
{
#if WINDOWS_UWP
    private const string WebBrokerReturnUrlTemplate = "ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}";
#endif
    private const string NativeClientRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
    private const string AuthorityTemplate = "https://login.microsoftonline.com/{0}";

    /// <summary>
    /// Return the redirect URL based on the package id of this app (UWP).
    /// This is needed in the app registration as redirect URI.
    /// For new UWP apps (or HoloLens apps) execute this function once to
    /// retrieve the URL so it can be registered in the client app registration
    /// as a return URL.
    /// </summary>
    /// <returns>The redirect URL.</returns>
    public static string GetRedirectUrl()
    {
        string url = "This can only be retrieved as app. Deploy the app to your HoloLens.";
#if WINDOWS_UWP
        url = string.Format(
        CultureInfo.InvariantCulture,
        WebBrokerReturnUrlTemplate,
        WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper(CultureInfo.InvariantCulture));
#endif
        return url;
    }

    /// <summary>
    /// Authenticate silent using Windows Account Manager (WAM) with the
    /// identity in the OS.
    /// </summary>
    /// <param name="clientId">Client ID from the app registration.</param>
    /// <param name="scopes">Scopes. Multiple scopes should be separated by a ' ' (space).
    /// Mostly this is the scope of the backend API, like "api://[client id]/user_impersonation".</param>
    /// <param name="resource">Resource to authenticate against. For the web api this
    /// is something like "api://[client id]"</param>
    /// <returns>Tuple with access token and username, or (null, null) on error.</returns>
    public static async Task<(string Token, string Username)> AuthenticateSilentWAMAsync(
        string clientId,
        string scopes,
        string resource)
    {
#if WINDOWS_UWP
        WebAccountProvider wap;
        WebTokenRequest wtr = null;

        try
        {
            wap = await WebAuthenticationCoreManager
                        .FindAccountProviderAsync("https://login.microsoft.com", "organizations")
                        .AsTask().ConfigureAwait(false);

            wtr = new WebTokenRequest(wap, scopes, clientId);
            wtr.Properties.Add("resource", resource);
        }
        catch (Exception ex)
        {
            Log.Err($"WAM.Request: {ex.Message}");
            return (null, null);
        }

        WebTokenRequestResult tokenResponse = null;

        try
        {
            tokenResponse = await WebAuthenticationCoreManager.GetTokenSilentlyAsync(wtr)
                                  .AsTask().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"WAM.GetToken: {ex.Message}");
        }

        if (tokenResponse.ResponseError != null)
        {
            Log.Err($"Error Code: {tokenResponse.ResponseError.ErrorCode}");
            Log.Err($"Error Msg: {tokenResponse.ResponseError.ErrorMessage}");
            Log.Err($"Error props:");
        }

        if (tokenResponse.ResponseStatus == WebTokenRequestStatus.Success)
        {
            if (tokenResponse.ResponseData.Count > 0)
            {
                WebTokenResponse resp = tokenResponse.ResponseData[0];

                string username = string.Empty;
                if (!resp.Properties.TryGetValue("DisplayName", out username))
                {
                    resp.Properties.TryGetValue("Name", out username);
                }

                return (resp.Token, username);
            }
        }

        return (null, null);
#else
		return (null, null);
#endif
    }

    /// <summary>
    /// Authenticate silent (if possible). It will use WAM under the hood
    /// and works both with app registrations for 1 org or multi org.
    /// It obtains the current user from the OS (UWP only).
    /// The AcquireTokenSilent call will first try to get the token from the MSAL
    /// owned cache. That also supports token refreshes.
    /// </summary>
    /// <param name="clientId">Client ID from the app registration.</param>
    /// <param name="tenantId">Tenant ID from the app registration.</param>
    /// <param name="scopes">Scopes. Multiple scopes should be separated by a ' ' (space).
    /// Mostly this is the scope of the backend API, like "api://[client id]/user_impersonation".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="AuthenticationResult"/> object or null on error.</returns>
    public static async Task<AuthenticationResult> AuthenticateSilentAsync(
        string clientId,
        string tenantId,
        string scopes,
        CancellationToken cancellationToken)
    {
        IPublicClientApplication app;
        try
        {
            // Authority URL is a defined start with addition of the tenant which can be 'common', 'organizations' or a tenant Id.
            string authority = string.Format(CultureInfo.InvariantCulture, AuthorityTemplate, tenantId);

            // Create the auth client using the broker of the OS to get the current user.
            app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(authority)
                .WithBroker()
                .WithWindowsBrokerOptions(new WindowsBrokerOptions
                {
                    ListWindowsWorkAndSchoolAccounts = true,
                })
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .Build();
        }
        catch (Exception ex)
        {
            Log.Err($"Unable to build the public client application! {ex.Message}");
            return null;
        }

        AuthenticationResult result = null;

        // Try to use the previously signed-in account from the cache
        IEnumerable<IAccount> accounts = await app.GetAccountsAsync();
        IAccount existingAccount = accounts.FirstOrDefault() ?? PublicClientApplication.OperatingSystemAccount;
        string[] requestedScopes = scopes.Split(' ');

        try
        {
            result = await app.AcquireTokenSilent(requestedScopes, existingAccount)
                              .ExecuteAsync()
                              .ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            Log.Err($"AuthSilent.Acquire.Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Authenticate with device code flow. The acquire request will return
    /// instructions to show to the user. It's essentially a message to go to
    /// a standard URL where the user has to enter the provided code. Once
    /// the user authenticates succesfully after that, the code returns with
    /// the logged in user.
    /// The caller has to register to the <see cref="DeviceCodeMessage"/> event
    /// to receive the instructions to be able to show it to the user.
    /// </summary>
    /// <param name="clientId">Client ID from the app registration.</param>
    /// <param name="clientId">Tenant ID from the app registration.</param>
    /// <param name="scopes">Scopes. Multiple scopes should be separated by a ' ' (space).
    /// Mostly this is the scope of the backend API, like "api://[client id]/user_impersonation".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="AuthenticationResult"/> object or null on error.</returns>
    public static async Task<AuthenticationResult> AuthenticateWithDeviceCodeAsync(
        string clientId,
        string tenantId,
        string scopes,
        Action<string> deviceCodeFlow,
        CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app;
        try
        {
            // Authority URL is a defined start with addition of the tenant which can be 'common', 'organizations' or a tenant Id.
            string authority = string.Format(CultureInfo.InvariantCulture, AuthorityTemplate, tenantId);

            // create the auth client
            // we'll be using a fixed redirect Uri that must be enabled in the
            // client app registration.
            app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithRedirectUri(NativeClientRedirectUri)
                .WithAuthority(authority)
                .Build();
        }
        catch (Exception ex)
        {
            Log.Err($"AuthDevCode.CreateApp.Exception: {ex.Message}");
            return null;
        }

        try
        {
            string[] scopesList = scopes.Split(' ');

            // Start the device code flow. This returns a message
            // that needs to be handled by the user. Once authenticated in a browser
            // on any device, the code will return here with an access token
            // if successful.
            AuthenticationResult result = await app.AcquireTokenWithDeviceCode(scopesList, flow =>
            {
                deviceCodeFlow?.Invoke(flow.Message);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            Log.Err($"AuthDevCode.Acquire.Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Authenticate interactive. The user will be prompted to enter the
    /// account credentials.
    /// NOTE: Do NOT use this on the HoloLens! Only on desktop.
    /// </summary>
    /// <param name="clientId">Client ID from the app registration.</param>
    /// <param name="clientId">Tenant ID from the app registration.</param>
    /// <param name="scopes">Scopes. Multiple scopes should be separated by a ' ' (space).
    /// Mostly this is the scope of the backend API, like "api://[client id]/user_impersonation".</param>
    /// <param name="cancellationTokenSource">Cancellation token.</param>
    /// <returns>A <see cref="AuthenticationResult"/> object or null on error.</returns>
    public static async Task<AuthenticationResult> AuthenticateInteractiveAsync(
        string clientId,
        string tenantId,
        string scopes,
        CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app;
        try
        {
            // Authority URL is a defined start with addition of the tenant which can be 'common', 'organizations' or a tenant Id.
            string authority = string.Format(CultureInfo.InvariantCulture, AuthorityTemplate, tenantId);

            // Create the auth client using the broker of the OS to get the current user.
            app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(authority)
                .WithBroker()
                .WithWindowsBrokerOptions(new WindowsBrokerOptions
                {
                    ListWindowsWorkAndSchoolAccounts = true,
                })
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .Build();
        }
        catch (Exception ex)
        {
            Log.Err($"Unable to build the public client application! {ex.Message}");
            return null;
        }

        AuthenticationResult result = null;

        // Try to use the previously signed-in account from the cache
        IEnumerable<IAccount> accounts = await app.GetAccountsAsync();

        // Get the logged in user from the OS.
        IAccount account = accounts.FirstOrDefault() ?? PublicClientApplication.OperatingSystemAccount;
        string[] requestedScopes = scopes.Split(' ');

        try
        {
            // Start the interactive login. This will popup UI to provide a user and password.
            result = await app.AcquireTokenInteractive(requestedScopes)
                .WithAccount(account)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            Log.Err($"AuthInteractive.Acquire.Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Removes all cached accounts.
    /// </summary>
    /// <param name="clientId">Client ID from the app registration.</param>
    /// <param name="tenantId">Tenant ID to authenticate against.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task SignOutAllAsync(
        string clientId,
        string tenantId)
    {
        // Authority URL is a defined start with addition of the tenant which can be 'common', 'organizations' or a tenant Id.
        string authority = string.Format(CultureInfo.InvariantCulture, AuthorityTemplate, tenantId);

        // create the auth client
        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithRedirectUri(NativeClientRedirectUri)
            .WithAuthority(authority)
            .Build();

        // get all cached accounts and remove them
        List<IAccount> accounts = (await app.GetAccountsAsync()).ToList();
        while (accounts.Count > 0)
        {
            await app.RemoveAsync(accounts[0]).ConfigureAwait(false);
            accounts = (await app.GetAccountsAsync().ConfigureAwait(false)).ToList();
        }
    }

    /// <summary>
    /// Validate current user with Windows Hello. It will popup the Hello interface
    /// as configured (pin, face, finger, iris) for the logged in user. When
    /// properly validated, we return true.
    /// If Windows Hello is not available, we'll return false.
    /// </summary>
    /// <param name="message">Message to show in the login window.</param>
    /// <returns>Validated true/false.</returns>
    public static async Task<bool> ValidateUserWithHelloAsync(string message = "Please verify your credentials")
    {
#if WINDOWS_UWP
        Log.Info($"Check if Hello is available.");
        if (await UserConsentVerifier.CheckAvailabilityAsync() == UserConsentVerifierAvailability.Available)
        {
            Log.Info($"Verify user.");
            UserConsentVerificationResult consentResult = await UserConsentVerifier.RequestVerificationAsync(message);
            Log.Info($"Result: {consentResult}");
            return consentResult == UserConsentVerificationResult.Verified;
        }
        else
        {
            Log.Err($"We don't have Hello access.");
            return false;
        }
#else
		Log.Info($"Standard: no validation.");
		return false;
#endif
    }
}
