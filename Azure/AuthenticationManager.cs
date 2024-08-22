// <copyright file="AuthenticationManager.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Azure.Security;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Nakamir.Common;
using StereoKit;

/// <summary>
/// The authentication manager can sign users in and retrieve an access token for API access.
/// </summary>
public class AuthenticationManager()
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="ApplicationManager"/>. The rest
    /// of the code in the application can just use AuthenticationManager.Instance.
    /// </summary>
    public static AuthenticationManager Instance { get; } = new();

    /// <summary>
    /// Gets the username. This is set once the auth is successful.
    /// </summary>
    public string Username { get; private set; }

    /// <summary>
    /// Gets the name of the user, if possible.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the Object ID of the user. Can be used for unique identification.
    /// </summary>
    public string ObjectId { get; private set; }

    /// <summary>
    /// Gets or sets the Client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Tenant ID.
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    public string Scopes { get; set; }

    /// <summary>
    /// This is a local cache of the token in case of using Device Code Flow.
    /// That mechanism can be used to login to another account then the standard
    /// account of the OS. The MSAL cache cannot handle that and it is for
    /// development use only anyway.
    /// </summary>
    public string AccessToken { get; private set; } = string.Empty;

    /// <summary>
    /// Get the access token for authorization use. When the <see cref="_accessToken"/>
    /// is set we'll return that (local cache), otherwise we'll get the token
    /// from the MSAL cache, which also handles token refresh in the cache.
    /// </summary>
    /// <returns>True if the sign in is successful.</returns>
    public async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        // get the token silently, probably from the cache
        AuthenticationResult result = await AuthenticationHelper.AuthenticateSilentAsync(ClientId, TenantId, Scopes, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            Log.Info("Couldn't retrieve access token from cache. Start authentication.");
            // retrieval from cache failed, so authenticate (again)
            result = await GetAuthenticationResultAsync(cancellationToken).ConfigureAwait(false);
        }

        if (result is not null)
        {
            GetUserDetails(result);
        }

        return result?.AccessToken;
    }

    /// <summary>
    /// Signs out all users in the current session.
    /// </summary>
    /// <returns>True if the sign is successful.</returns>
    public async Task<bool> SignOutAllAsync()
    {
        bool success = false;
        try
        {
            await AuthenticationHelper.SignOutAllAsync(ClientId, TenantId);
            success = true;
        }
        catch (Exception ex)
        {
            Log.Err($"Error signing out: {ex.Message}");
        }
        return success;
    }

    /// <summary>
    /// Trigger the device code flow and get an access token.
    /// </summary>
    /// <returns>The access token.</returns>
    private async Task<AuthenticationResult> GetAuthenticationResultAsync(CancellationToken cancellationToken)
    {
        // Handle the device code flow message
        static async void DeviceCodeFlow(string message)
        {
            // Get the code from the message
            int start = message.IndexOf("the code") + 9;
            int end = message.IndexOf("to auth");
            string code = message.Substring(start, end - start - 1);

#if WINDOWS_UWP
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new("https://microsoft.com/devicelogin")).AsTask().ConfigureAwait(false);
            }).AsTask().ConfigureAwait(false);
#else
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://microsoft.com/devicelogin",
                UseShellExecute = true
            });
#endif

            TextDisplay.PushText(code);
            Log.Info(message);
        }

        // Get the user using device code flow message with link and code will appear
        AuthenticationResult result = await AuthenticationHelper.AuthenticateWithDeviceCodeAsync(
                            ClientId,
                            TenantId,
                            Scopes,
                            DeviceCodeFlow,
                            cancellationToken).ConfigureAwait(false);

        TextDisplay.PopText();
        return result;
    }

    private void GetUserDetails(AuthenticationResult result)
    {
        // Get some user details from the acquired token
        ObjectId = result.Account.HomeAccountId.ObjectId;
        if (string.IsNullOrEmpty(ObjectId))
        {
            ObjectId = result.Account.Username;
        }
        Log.Info($"Object ID: {ObjectId.Substring(0, 4)}***");

        // Get the username from the token
        Username = result.Account.Username;
        Log.Info($"Username: {Username}");

        // Try to get a real name from the token
        System.Security.Claims.Claim claim = result.ClaimsPrincipal.FindFirst("name");
        if (claim is not null)
        {
            Name = claim.Value;
        }
        else
        {
            // Otherwise use the default username (probably the email address)
            Name = result.Account.Username;
        }
        Log.Info($"Name: {Name}");
    }
}
