// <copyright file="StereoKitLogger.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace SKAzureCloud;

using Nakamir.Security;

public class StereoKitLogger(ILogContext context) : IAADLogger
{
    public void Clear()
    {
        context.ClearLog();
    }

    public void Log(string msg, bool toConsole = true)
    {
        context.QueueLog(msg, toConsole);
    }
}
