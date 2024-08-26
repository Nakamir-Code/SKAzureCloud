// <copyright file="IUserStore.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

public interface IUserStore
{
    public void SaveUser(string key, string userId);

    public string GetUserId(string key);

    public void ClearUser(string key);
}
