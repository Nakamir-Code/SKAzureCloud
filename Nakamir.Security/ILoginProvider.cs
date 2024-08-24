// <copyright file="ILoginProvider.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

﻿using System.Threading.Tasks;

public interface ILoginProvider
{
    public string UserIdKey { get; }
    public string LogContent { get; }
    public string AADToken { get; }
    public string AccessToken { get; }
    public string Username { get; }
    public bool IsSignedIn { get; }

    public byte[] UserPicture { get; set; }

    public Task<IToken> LoginAsync(string[] scopes);

    public Task SignOutAsync();

    public void Log(string msg);
    public void ClearLog();
    public string Description { get; }
    public string ProviderName { get; }
}
