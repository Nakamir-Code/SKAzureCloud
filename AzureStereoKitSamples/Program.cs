using Microsoft.Identity.Client;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph;
using System.Net.Http.Headers;
using System.Text;
using Azure.Storage.Blobs;

namespace AzureStereoKitSamples
{
    internal class Program
    {
        // Set the scopes for API 
        private static string[] _scopesGraph = new string[] { "https://graph.microsoft.com/User.Read" };
        private static string[] _scopesStorage = new string[] { "https://storage.azure.com/user_impersonation" };
        
        // Setup the URLs
        private static string _storageAccountUrl = "https://nakamirstoragedemo1.blob.core.windows.net/";
        private static string _MSGraphURL = "https://graph.microsoft.com/v1.0/";

        // Below are the clientId (Application Id) of your app registration and the tenant information.
        // You have to replace the clientId and tenantId:
        private const string _clientId = "77fe1124-4d7d-497c-8440-4f8d1c36e09b";
        private const string _tenantId = "ad853c1d-e755-42bb-92fb-c8ae3c392583"; // Alternatively "[Enter your tenant, as obtained from the Azure portal, e.g. kko365.onmicrosoft.com]"
        private const string _authority = "https://login.microsoftonline.com/" + _tenantId;

        // The MSAL Public client app
        private static IPublicClientApplication _publicClientApp;
        

        private static string _resultText = String.Empty;
        private static Pose _menuPose = new Pose(Input.Head.Forward * 0.5f, Quat.LookAt(Input.Head.Forward, Input.Head.position));
        enum MenuState
        {
            SignedOut,
            SigningIn,
            SignedIn,
            SigningOut
        };

        static void Main(string[] args)
        {
            // Initialize StereoKit
            SKSettings settings = new SKSettings
            {
                appName = "AzureStereoKitSamples",
                assetsFolder = "Assets",
            };
            if (!SK.Initialize(settings))
                Environment.Exit(1);

            Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
            Material floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
            floorMaterial.Transparency = Transparency.Blend;
            MenuState menuState = MenuState.SignedOut;

            // Core application loop
            while (SK.Step(() =>
            {
                UI.WindowBegin("Azure Menu", ref _menuPose, new Vec2(0.5f, 0));
                if (menuState == MenuState.SignedOut)
                {
                    if (UI.Button("Get Graph Data"))
                    {
                        menuState = MenuState.SigningIn;
                        TryGetGraphDataAsync().ContinueWith((t) =>
                        {
                            if (t.Result)
                            {
                                menuState = MenuState.SignedIn;
                            }
                            else
                            {
                                menuState = MenuState.SignedOut;
                            }
                        });
                    }
                    else if (UI.Button("Upload Blob"))
                    {
                        menuState = MenuState.SigningIn;
                        TryUploadBlobAsync().ContinueWith((t) =>
                        {
                            if (t.Result)
                            {
                                menuState = MenuState.SignedIn;
                            }
                            else
                            {
                                menuState = MenuState.SignedOut;
                            }
                        });
                    }
                    else if (UI.Button("Exit"))
                    {
                        SK.Quit();
                    }
                }
                else if (menuState == MenuState.SigningIn)
                {
                    UI.Text("Signing In...");
                }
                else if (menuState == MenuState.SignedIn)
                {
                    UI.Text(_resultText);

                    if (UI.Button("Sign Out"))
                    {
                        menuState = MenuState.SigningOut;
                        _resultText = String.Empty;

                        TrySignOutAsync().ContinueWith((t) =>
                        {
                            if (t.Result)
                            {
                                menuState = MenuState.SignedOut;
                            }
                            else
                            {
                                menuState = MenuState.SignedIn;
                            }
                        });
                    }
                }
                else if (menuState == MenuState.SigningOut)
                {
                    UI.Text("Signing Out...");
                }

                UI.WindowEnd();

            })) ;
            SK.Shutdown();
        }

        /// <summary>
        /// Gets token using AAD and uses it for accessing Microsoft Graph API.
        /// Upon successful completion, updates the _resultText which is 
        /// later used to display the acquired data from the MS Graph API.
        /// </summary>
        /// <returns>bool, returns true if successfully completed.</returns>
        public static async Task<bool> TryGetGraphDataAsync()
        {
            bool success = false;
            try
            {
                // Sign-in user using MSAL and obtain an access token for MS Graph
                GraphServiceClient graphClient = await SignInAndInitializeGraphServiceClientAsync(_scopesGraph);

                // Call the /me endpoint of Graph
                User graphUser = await graphClient.Me.Request().GetAsync();

                // Go back to the UI thread to make changes to the UI
                _resultText = "Display Name: " + graphUser.DisplayName + "\nBusiness Phone: " + graphUser.BusinessPhones.FirstOrDefault()
                  + "\nGiven Name: " + graphUser.GivenName + "\nid: " + graphUser.Id
                  + "\nUser Principal Name: " + graphUser.UserPrincipalName;
                Debug.WriteLine(_resultText);
                success = true;
            }
            catch (MsalException msalEx)
            {
                Debug.WriteLine($"Error Acquiring Token:{System.Environment.NewLine}{msalEx}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
            }

            return success;
        }

        /// <summary>
        /// Gets token using AAD and uses it to access Microsoft Blob Storage.
        /// After uploading a text file to a predefined container, it then reads the content of the text file.
        /// </summary>
        /// <returns>bool, returns true upon successful completion.</returns>
        private static async Task<bool> TryUploadBlobAsync()
        {
            bool success = false;
            try
            {
                var token = await SignInUserAndGetTokenUsingMSAL(_scopesStorage);
                var sb = new StringBuilder();
                BlobServiceClient service = new BlobServiceClient(new Uri(_storageAccountUrl), new StringTokenCredential(token));
                BlobContainerClient container = service.GetBlobContainerClient("targetcontainer");
                
                if (!await container.ExistsAsync())
                {
                    var containerResponse = await container.CreateAsync();
                    sb.AppendLine($"Created container:{container.Name} at{containerResponse.Value.LastModified}");
                }
                
                var blob = container.GetBlobClient(Guid.NewGuid().ToString());
                //if (!await blob.ExistsAsync())
                //{
                var uploadResponse = await blob.UploadAsync(new BinaryData(Guid.NewGuid().ToByteArray()));
                sb.AppendLine($"Created container:{blob.Name} at{uploadResponse.Value.LastModified}");
                //}
                var content = await blob.DownloadContentAsync();
                var contentGuid = new Guid(content.Value.Content.ToArray());
                sb.AppendLine($"File {blob.Name},  Content:{contentGuid}");
                
                _resultText = sb.ToString();
                Debug.WriteLine(_resultText);
                success = true;
            }
            catch (MsalException msalEx)
            {
                Debug.WriteLine($"Error Acquiring Token:{System.Environment.NewLine}{msalEx}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error {nameof(TryUploadBlobAsync)}:{System.Environment.NewLine}{ex}");
            }

            return success;
        }

        /// <summary>
        /// Sign in user using MSAL and obtain a token for Microsoft Graph
        /// </summary>
        /// <returns>GraphServiceClient</returns>
        private async static Task<GraphServiceClient> SignInAndInitializeGraphServiceClientAsync(string[] scopes)
        {
            GraphServiceClient graphClient = new GraphServiceClient(_MSGraphURL,
                new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", await SignInUserAndGetTokenUsingMSAL(scopes));
                }));

            return graphClient;
        }


        /// <summary>
        /// Signs in the user and obtains an access token
        /// </summary>
        /// <param name="scopes"></param>
        /// <returns> Access Token</returns>
        private static async Task<string> SignInUserAndGetTokenUsingMSAL(string[] scopes)
        {
            // Initialize the MSAL library by building a public client application
            _publicClientApp = PublicClientApplicationBuilder.Create(_clientId)
                .WithAuthority(_authority)
                .WithUseCorporateNetwork(false)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                 .WithLogging((level, message, containsPii) =>
                 {
                     Debug.WriteLine($"MSAL: {level} {message} ");
                 }, Microsoft.Identity.Client.LogLevel.Warning, enablePiiLogging: false, enableDefaultPlatformLogging: true)
                .Build();

            // It's good practice to not do work on the UI thread, so use ConfigureAwait(false) whenever possible.
            IEnumerable<IAccount> accounts = await _publicClientApp.GetAccountsAsync().ConfigureAwait(false);
            IAccount firstAccount = accounts.FirstOrDefault();

            AuthenticationResult authResult;

            try
            {
                authResult = await _publicClientApp.AcquireTokenSilent(scopes, firstAccount)
                                                  .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenInteractive to acquire a token
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");


                authResult = await _publicClientApp.AcquireTokenInteractive(scopes)
                                            .ExecuteAsync()
                                            .ConfigureAwait(false);
            }
            return authResult.AccessToken;
        }

        /// <summary>
        /// Sign out the current user
        /// </summary>
        private static async Task<bool> TrySignOutAsync()
        {
            IEnumerable<IAccount> accounts = await _publicClientApp.GetAccountsAsync().ConfigureAwait(false);
            IAccount firstAccount = accounts.FirstOrDefault();

            bool success = false;
            try
            {
                await _publicClientApp.RemoveAsync(firstAccount).ConfigureAwait(false);

                // user signs out dialogue
                Debug.WriteLine("User has signed-out");
                success = true;
            }
            catch (MsalException ex)
            {
                Debug.WriteLine($"Error signing-out user: {ex.Message}");
            }

            return success;
        }

    }
}
