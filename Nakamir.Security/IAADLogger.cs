// <copyright file="IAADLogger.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;
﻿
public interface IAADLogger
{
    public void Log(string msg, bool toConsole = false);

    public void Clear();
}
