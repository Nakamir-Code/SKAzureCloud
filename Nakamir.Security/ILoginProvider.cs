// <copyright file="ILoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

using System.Threading.Tasks;

/// <summary>
/// Represents a login provider to authenticate users with the appropriate scopes.
/// </summary>
public interface ILoginProvider
{
    /// <summary>
    /// Gets the name of the login provider.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the description of the login provider.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets a key to later identify the user's account.
    /// </summary>
    public string UserIdKey { get; }

    /// <summary>
    /// Gets the access token provided by a successful login.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets the username of the logged in user.
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// Attempts to log the user into their account with the appropriate scopes.
    /// </summary>
    /// <param name="scopes">The scopes to authorize the user to the service. For example, 'User.Read' is
    /// a necessary scope to acquire Microsoft Graph account information.</param>
    /// <returns></returns>
    public Task<string> LoginAsync(string[] scopes = null);

    /// <summary>
    /// Attempts to the sign the user out.
    /// </summary>
    /// <returns></returns>
    public Task LogoutAsync();

    /// <summary>
    /// Clears the cache for this provider for future login attempts.
    /// </summary>
    public void ClearUser();
}
