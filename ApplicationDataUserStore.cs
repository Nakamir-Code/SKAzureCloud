// <copyright file="ApplicationDataUserStore.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace SKAzureCloud;

using Windows.Storage;
using Nakamir.Security;

public class ApplicationDataUserStore : IUserStore
{
    public void SaveUser(string key, string userId)
    {
        ApplicationData.Current.RoamingSettings.Values[key] = userId;
    }

    public string GetUserId(string key)
    {
        return (string)ApplicationData.Current.RoamingSettings.Values[key];
    }

    public void ClearUser(string key)
    {
        ApplicationData.Current.RoamingSettings.Values[key] = null;
    }
}
