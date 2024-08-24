// <copyright file="ILogContext.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace SKAzureCloud;
﻿
public interface ILogContext
{
    void ClearLog();

    void QueueLog(string msg, bool toConsole);
}
