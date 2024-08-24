// <copyright file="AADToken.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Security;

public class AADToken(string token) : IToken
{
    public string Token { get; private set; } = token;
}
