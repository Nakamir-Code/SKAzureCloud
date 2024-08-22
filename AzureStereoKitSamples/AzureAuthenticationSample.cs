// <copyright file="AzureAuthenticationSample.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Azure.Security;

using System;
using System.Threading;
using System.Threading.Tasks;
using global::Azure.Core;
using global::Azure.Storage.Blobs;
using Microsoft.Identity.Client;
using Nakamir.Common;
using StereoKit;
using LogLevel = StereoKit.LogLevel;

internal class AzureAuthenticationSample
{
    // Below is the client identifier (Application Id) of your app registration and the tenant information.
    // Please replace the following constants:
    private const string ClientId = "bafda199-b430-450b-b9fa-eeca607e5438";
    private const string TenantId = "common";// "ad853c1d-e755-42bb-92fb-c8ae3c392583"; // Alternatively "[Enter your tenant, as obtained from the Azure portal, e.g. nakamir.onmicrosoft.com]"
    private const string BlobUrl = "https://your-storage-account.blob.core.windows.net/container-name/blob-name";

    private static string _resultText = string.Empty;
    private static Pose _menuPose = new(Input.Head.Forward * 0.5f, Quat.LookAt(Input.Head.Forward, Input.Head.position));
    private static MenuState _menuState = MenuState.SignedOut;
    enum MenuState
    {
        SignedOut,
        SigningIn,
        SignedIn,
        SigningOut,
        UploadingBlob,
    };

    internal static void Main(string[] _)
    {
        // Initialize StereoKit
        SKSettings settings = new()
        {
            appName = nameof(AzureAuthenticationSample),
            assetsFolder = "Assets",
        };
        if (!SK.Initialize(settings)) { Environment.Exit(1); }
#if DEBUG
        Log.Subscribe((level, text) => System.Diagnostics.Debug.WriteLine($"[SK {level.ToString()}]: {text}"));
#endif

        Matrix floorTransform = Matrix.TS(0, -1.5f, 0, V.XYZ(30, 0.1f, 30));
        Material floorMaterial = new(Shader.FromFile("floor.hlsl"))
        {
            Transparency = Transparency.Blend
        };

        Log.Info(AuthenticationHelper.GetRedirectUrl());

        AuthenticationManager.Instance.ClientId = ClientId;
        AuthenticationManager.Instance.TenantId = TenantId;

        CancellationTokenSource cancellationToken = new();
        try
        {
            // Core application loop
            SK.Run(() =>
            {
                UI.WindowBegin("Azure Menu", ref _menuPose, new Vec2(0.5f, 0));
                if (_menuState != MenuState.SigningIn && _menuState != MenuState.SigningOut)
                {
                    if (UI.Button("Authenticate User"))
                    {
                        _menuState = MenuState.SigningIn;
                        cancellationToken ??= new();
                        AuthenticationManager.Instance.Scopes = "User.Read";// string.Empty;
                        AuthenticationManager.Instance.SignInAsync(cancellationToken.Token).SafeFireAndCallback(result => SK.ExecuteOnMain(() => _menuState = !string.IsNullOrEmpty(result) ? MenuState.SignedIn : MenuState.SignedOut));
                    }
                    else if (UI.Button("Upload Blob"))
                    {
                        _menuState = MenuState.UploadingBlob;
                        cancellationToken ??= new();
                        AuthenticationManager.Instance.Scopes = "https://storage.azure.com/.default";
                        UploadBlobAsync(BlobUrl, cancellationToken.Token).SafeFireAndCallback(() => SK.ExecuteOnMain(() => _menuState = MenuState.SignedIn));
                    }
                }

                if (_menuState == MenuState.SigningIn)
                {
                    if (UI.Button("Cancel"))
                    {
                        cancellationToken.Cancel();
                        cancellationToken.Dispose();
                        cancellationToken = null;
                    }
                }

                if (_menuState == MenuState.SignedIn && UI.Button("Sign Out"))
                {
                    _menuState = MenuState.SigningOut;
                    _resultText = string.Empty;
                    AuthenticationManager.Instance.SignOutAllAsync().SafeFireAndCallback(success => SK.ExecuteOnMain(() => _menuState = success ? MenuState.SignedOut : MenuState.SignedIn));
                }

                if (UI.Button("Exit"))
                {
                    SK.Quit();
                }

                string status = _menuState switch
                {
                    MenuState.SignedOut => "Signed Out",
                    MenuState.SigningIn => "Signing In...",
                    MenuState.SignedIn => "Signed in as " + AuthenticationManager.Instance.Username,
                    MenuState.SigningOut => "Signing Out...",
                    _ => string.Empty,
                };
                UI.Text(status);
                if (!string.IsNullOrEmpty(_resultText))
                {
                    UI.Text(_resultText);
                }
                UI.WindowEnd();
            });
        }
        finally
        {
            cancellationToken?.Dispose();
        }
    }

    private static void LogMessage(LogLevel logLevel, string msg)
    {
        SK.ExecuteOnMain(() => _resultText = msg);
        Log.Write(logLevel, msg);
    }

    /// <summary>
    /// Gets token using MSAL and uses it to access Azure Blob Storage.
    /// After uploading a text file to a predefined container, it reads the content of the text file.
    /// </summary>
    /// <returns>True if the blob uploaded and downloaded successfullly.</returns>
    private static async Task<bool> UploadBlobAsync(string blobUrl, CancellationToken cancellationToken)
    {
        bool success = false;
        try
        {
            string accessToken = await AuthenticationManager.Instance.SignInAsync(cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("Could not sign in!");
            }

            TokenCredential tokenCredential = new AccessTokenCredential(accessToken);
            BlobClient blobClient = new(new(blobUrl), tokenCredential);
            await blobClient.DeleteIfExistsAsync();

            // Upload content
            var uploadResponse = await blobClient.UploadAsync(new BinaryData(Guid.NewGuid().ToByteArray()), cancellationToken);
            LogMessage(LogLevel.Info, $"Created container:{blobClient.Name} at{uploadResponse.Value.LastModified}");

            // Download content to verify
            var content = await blobClient.DownloadContentAsync(cancellationToken);
            var contentGuid = new Guid(content.Value.Content.ToArray());
            LogMessage(LogLevel.Info, $"File: {blobClient.Name},  Content: {contentGuid}");
            success = true;
        }
        catch (MsalException msalEx)
        {
            LogMessage(LogLevel.Error, $"Error Acquiring Token:{Environment.NewLine}{msalEx}");
        }
        catch (Exception ex)
        {
            LogMessage(LogLevel.Error, $"Error {nameof(UploadBlobAsync)}:{Environment.NewLine}{ex}");
        }
        return success;
    }

    private class AccessTokenCredential(string accessToken) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken(accessToken, DateTimeOffset.MaxValue);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(new AccessToken(accessToken, DateTimeOffset.MaxValue));
        }
    }
}
