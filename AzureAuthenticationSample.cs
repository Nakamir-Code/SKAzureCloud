// <copyright file="AzureAuthenticationSample.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace SKAzureCloud;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using global::Azure.Core;
using global::Azure.Storage.Blobs;
using Nakamir.Common;
using Nakamir.Security;
using Nakamir.Security.LoginProviders;
using StereoKit;
using StereoKit.Framework;
using Windows.ApplicationModel.Core;
using Windows.System;

internal class AzureAuthenticationSample
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// REPLACE THE PARAMETERS BELOW                                                                                               ///
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private const string ClientId = "bafda199-b430-450b-b9fa-eeca607e5438";
    private const string TenantId = "ad853c1d-e755-42bb-92fb-c8ae3c392583";
    private const string CustomTenantId = "common"; // Alternatively "[Enter your tenant, as obtained from the Azure portal, e.g. nakamir.onmicrosoft.com]"
    private const string Resource = "organizations";
    private const string CustomResource = "consumers";
    private const string BlobUrl = "https://your-storage-account.blob.core.windows.net/container-name/blob-name";
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static bool UseCustomTenantId;
    private static bool UseCustomResource;
    private static string _resultText = string.Empty;
    private static Pose _menuPose = new(Input.Head.Forward * 0.5f, Quat.LookAt(Input.Head.Forward, Input.Head.position));
    private static MenuState _menuState = MenuState.SignedOut;
    private enum MenuState
    {
        SignedOut,
        SigningIn,
        SignedIn,
        SigningOut,
        BlobStorageTest,
    };

    private static LoginProviderType _currentProviderType = LoginProviderType.MSAL;
    private enum LoginProviderType
    {
        MSAL,
        WAB,
        WAM,
        WAMWAB,
        WAP
    };

    public IUserStore UserStore { get; } = new ApplicationDataUserStore();
    public ILoginProvider CurrentLoginProvider { get; set; }

    internal static void Main(string[] _)
    {
        AzureAuthenticationSample sample = new();
        void ChangeLoginProvider(LoginProviderType providerType)
        {
            string tenantId = UseCustomTenantId ? CustomTenantId : TenantId;
            string resource = UseCustomResource ? CustomResource : Resource;
            sample.CurrentLoginProvider = providerType switch
            {
                LoginProviderType.MSAL => new MSALLoginProvider(sample.UserStore, ClientId, tenantId,
                redirectUri: "https://login.microsoftonline.com/common/oauth2/nativeclient", () =>
                {
                    dynamic coreWindow = CoreApplication.MainView.CoreWindow;
                    ICoreWindowInterop interop = (ICoreWindowInterop)coreWindow;
                    return interop.WindowHandle;
                }),
                LoginProviderType.WAB => new WABLoginProvider(sample.UserStore, ClientId, tenantId),
                LoginProviderType.WAM => new WAMLoginProvider(sample.UserStore, ClientId, tenantId, resource, biometricsRequired: false),
                LoginProviderType.WAMWAB => new WAMWABLoginProvider(sample.UserStore, ClientId, tenantId, resource),
                LoginProviderType.WAP => new WAPLoginProvider(sample.UserStore),
                _ => throw new NotImplementedException($"Login provider for {providerType} is not implemented.")
            };
        }
        ChangeLoginProvider(_currentProviderType);

        // Initialize StereoKit
        SKSettings settings = new()
        {
            appName = nameof(AzureAuthenticationSample),
            assetsFolder = "Assets",
        };
        if (!SK.Initialize(settings)) { Environment.Exit(1); }

        SK.AddStepper<LogWindow>();

        Matrix floorTransform = Matrix.TS(0, -1.5f, 0, V.XYZ(30, 0.1f, 30));
        Material floorMaterial = new(Shader.FromFile("floor.hlsl"))
        {
            Transparency = Transparency.Blend
        };

        string selectedUsername = string.Empty;
        Dictionary<string, string> foundUsers = [];

        // Watches user events on the device.
        var userWatcher = User.CreateWatcher();
        userWatcher.Added += async (_, args) =>
        {
            string username = await args.User.GetPropertyAsync(KnownUserProperties.AccountName) as string;
            SK.ExecuteOnMain(() => foundUsers.TryAdd(args.User.NonRoamableId, username));
        };
        userWatcher.Updated += async (_, args) =>
        {
            string username = await args.User.GetPropertyAsync(KnownUserProperties.AccountName) as string;
            SK.ExecuteOnMain(() => { if (foundUsers.ContainsKey(args.User.NonRoamableId)) foundUsers[args.User.NonRoamableId] = username; });
        };
        userWatcher.Removed += async (_, args) =>
        {
            string username = await args.User.GetPropertyAsync(KnownUserProperties.AccountName) as string;
            SK.ExecuteOnMain(() => { if (foundUsers.ContainsKey(args.User.NonRoamableId)) foundUsers.Remove(args.User.NonRoamableId); });
        };
        userWatcher.Start();

        CancellationTokenSource cancellationToken = new();
        try
        {
            // Core application loop
            SK.Run(() =>
            {
                UI.WindowBegin("Azure Menu", ref _menuPose, new Vec2(0.53f, 0));

                UI.PushEnabled(_menuState == MenuState.SignedOut);
                UI.Label("Provider:  ");
                UI.SameLine();
                if (UI.Button(sample.CurrentLoginProvider.ProviderName))
                {
                    // Toggle through the LoginProviderType enum values
                    _currentProviderType = _currentProviderType switch
                    {
                        LoginProviderType.MSAL => LoginProviderType.WAB,
                        LoginProviderType.WAB => LoginProviderType.WAM,
                        LoginProviderType.WAM => LoginProviderType.WAMWAB,
                        LoginProviderType.WAMWAB => LoginProviderType.WAP,
                        LoginProviderType.WAP => LoginProviderType.MSAL,
                        _ => _currentProviderType // Fallback in case no match (though this shouldn't happen)
                    };
                    ChangeLoginProvider(_currentProviderType);
                }

                UI.Label("Tenant:    ");
                UI.SameLine();
                if (UI.Button(UseCustomTenantId ? CustomTenantId : TenantId))
                {
                    UseCustomTenantId = !UseCustomTenantId;
                    ChangeLoginProvider(_currentProviderType);
                }

                if (_currentProviderType == LoginProviderType.WAM || _currentProviderType == LoginProviderType.WAMWAB)
                {
                    UI.Label("Resource:");
                    UI.SameLine();
                    if (UI.Button(UseCustomResource ? CustomResource : Resource))
                    {
                        UseCustomResource = !UseCustomResource;
                        ChangeLoginProvider(_currentProviderType);
                    }
                }

                if (_currentProviderType == LoginProviderType.MSAL && sample.CurrentLoginProvider is MSALLoginProvider msalLoginProvider)
                {
                    UI.Label("Use Device Code Flow?");
                    UI.SameLine();
                    bool useDeviceCodeFlow = msalLoginProvider.UseDeviceCodeFlow;
                    if (UI.Toggle(useDeviceCodeFlow ? "Yes" : "No", ref useDeviceCodeFlow))
                    {
                        msalLoginProvider.UseDeviceCodeFlow = useDeviceCodeFlow;
                    }

                    UI.Label("Use Embedded or System UI?");
                    UI.SameLine();
                    bool useEmbedded = msalLoginProvider.UseEmbedded;
                    if (UI.Button(useEmbedded ? "Embedded" : "System"))
                    {
                        msalLoginProvider.UseEmbedded = !msalLoginProvider.UseEmbedded;
                    }
                }
                UI.PopEnabled();

                if (_menuState != MenuState.SigningIn && _menuState != MenuState.SigningOut)
                {
                    if (UI.Button("Authenticate User"))
                    {
                        _menuState = MenuState.SigningIn;
                        cancellationToken ??= new();
                        if (sample.CurrentLoginProvider is WAMLoginProvider wamLoginProvider)
                        {
                            wamLoginProvider.Resource = "https://graph.microsoft.com";
                        }
                        else if (sample.CurrentLoginProvider is WAMWABLoginProvider wamWabLoginProvider)
                        {
                            wamWabLoginProvider.Resource = "https://graph.microsoft.com";
                        }
                        sample.CurrentLoginProvider.LoginAsync(["User.Read"]).SafeFireAndCallback(result => SK.ExecuteOnMain(()
                            => _menuState = string.IsNullOrEmpty(result) ? MenuState.SignedOut : MenuState.SignedIn));
                    }
                    else if (UI.Button("Upload Blob"))
                    {
                        _menuState = MenuState.SigningIn;
                        cancellationToken ??= new();
                        if (sample.CurrentLoginProvider is WAMLoginProvider wamLoginProvider)
                        {
                            wamLoginProvider.Resource = "https://storage.azure.com";
                        }
                        else if (sample.CurrentLoginProvider is WAMWABLoginProvider wamWabLoginProvider)
                        {
                            wamWabLoginProvider.Resource = "https://storage.azure.com";
                        }
                        sample.UploadBlobAsync(BlobUrl, ["https://storage.azure.com/user_impersonation"], cancellationToken.Token).SafeFireAndCallback(result =>
                            SK.ExecuteOnMain(() => _menuState = result ? MenuState.SignedIn : MenuState.SignedOut));
                    }

                    if (foundUsers.Count > 0 && _currentProviderType == LoginProviderType.MSAL && sample.CurrentLoginProvider is MSALLoginProvider loginHintProvider)
                    {
                        UI.Label("Select an account below to be the login hint:");
                        foreach (string foundUser in foundUsers.Values)
                        {
                            bool isUser = !string.IsNullOrEmpty(loginHintProvider.LoginHint) && foundUser.Equals(loginHintProvider.LoginHint, StringComparison.Ordinal);
                            if (UI.Toggle(foundUser, ref isUser))
                            {
                                loginHintProvider.LoginHint = isUser ? foundUser : null;
                            }
                        }
                    }
                }

                if (_menuState == MenuState.SigningIn && UI.Button("Cancel"))
                {
                    cancellationToken.Cancel();
                    cancellationToken.Dispose();
                    cancellationToken = null;
                }

                if (_menuState == MenuState.SignedIn && UI.Button("Sign Out"))
                {
                    _menuState = MenuState.SigningOut;
                    _resultText = string.Empty;
                    sample.CurrentLoginProvider.LogoutAsync().SafeFireAndCallback(() =>
                        SK.ExecuteOnMain(() => _menuState = string.IsNullOrEmpty(sample.CurrentLoginProvider.AccessToken) ? MenuState.SignedOut : MenuState.SignedIn));
                }

                if (UI.Button("Exit"))
                {
                    SK.Quit();
                }

                string status = _menuState switch
                {
                    MenuState.SignedOut => "Signed Out",
                    MenuState.SigningIn => "Signing In...",
                    MenuState.SignedIn => "Signed in as " + sample.CurrentLoginProvider.Username,
                    MenuState.SigningOut => "Signing Out...",
                    MenuState.BlobStorageTest => "Uploading and downloading blob...",
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
            userWatcher.Stop();
        }
    }

    /// <summary>
    /// Gets token using MSAL and uses it to access Azure Blob Storage.
    /// After uploading a text file to a predefined container, it reads the content of the text file.
    /// </summary>
    /// <returns>True if the blob uploaded and downloaded successfully.</returns>
    private async Task<bool> UploadBlobAsync(string blobUrl, string[] scopes, CancellationToken cancellationToken = default)
    {
        bool success = false;
        try
        {
            string accessToken = await CurrentLoginProvider.LoginAsync(scopes);
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("Could not sign in!");
            }
            SK.ExecuteOnMain(() => _menuState = MenuState.BlobStorageTest);

            TokenCredential tokenCredential = new AccessTokenCredential(accessToken);
            BlobClient blobClient = new(new(blobUrl), tokenCredential);
            await blobClient.DeleteIfExistsAsync();

            // Upload content
            var uploadResponse = await blobClient.UploadAsync(new BinaryData(Guid.NewGuid().ToByteArray()), cancellationToken);
            Log.Info($"Created container:{blobClient.Name} at{uploadResponse.Value.LastModified}");

            // Download content to verify
            var content = await blobClient.DownloadContentAsync(cancellationToken);
            var contentGuid = new Guid(content.Value.Content.ToArray());
            Log.Info($"File: {blobClient.Name},  Content: {contentGuid}");
            success = true;
        }
        catch (Exception ex)
        {
            Log.Err($"Error {nameof(UploadBlobAsync)}:{Environment.NewLine}{ex}");
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

    [ComImport, Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICoreWindowInterop
    {
        public IntPtr WindowHandle { get; }
        public bool MessageHandled { set; }
    }
}
