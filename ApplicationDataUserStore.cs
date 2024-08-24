// <copyright file="ApplicationDataUserStore.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace SKAzureCloud;

using Nakamir.Security;
using Windows.Storage;

public class ApplicationDataUserStore : IUserStore
{
    public void ClearUser(string key)
    {
        ApplicationData.Current.RoamingSettings.Values[key] = null;
    }

    public string GetUserId(string key)
    {
        return (string)ApplicationData.Current.RoamingSettings.Values[key];
    }

    public void SaveUser(string key, string userId)
    {
        ApplicationData.Current.RoamingSettings.Values[key] = userId;
    }
}
