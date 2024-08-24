# SKAzureCloud
A [StereoKit](https://stereokit.net/) sample application for UWP devices (such as the Microsoft HoloLens 2) 
that demonstrates user authentication to Microsoft Azure Active Directory from the following providers:
- Microsoft Authentication Library (MSAL)
- Web Authentication Broker (WAB)
- Web Authentication Manager (WAM)
- Windows Account Provider (WAP)

## Features
* Authenticates users based on the chosen authentication provider and its settings.
* Acquires an access token to upload a blob to Azure Blob Storage. Then downloads and displays the content.
* Attempts to login silently, then falls back to either an interactive or device code flow token acquisition.
* Watches for users that are being added, updated, or removed from the current device.

As this code was inspired by the [https://github.com/peted70/aad-hololens](aad-hololens) Unity project, please read Pete's extremely helpful overview there first!

The following samples may also help with the necessary setup in the Azure Portal:
- https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-app?tabs=dotnet
- https://learn.microsoft.com/en-us/azure/active-directory/develop/tutorial-v2-windows-uwp

## Usage
In the code, edit both the Client ID and Tenant ID according to your application registration credentials. 
Also, edit the storage account URL according to you Azure Blob Storage setup.

## Package Versioning
UWP is a dying platform, so it makes sense that Microsoft.Identity.Client [discontinued support for UWP](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/4427#issuecomment-2119973049). Thus, we require the latest non-vulnerable package version: <b>4.47.2</b>. For non-UWP applications, most of the code is similar, except you'll need to make minor changes to the broker options for token acquisition.
