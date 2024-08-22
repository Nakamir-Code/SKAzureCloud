# Azure StereoKit UWP Samples
What’s the first thing that anyone creating any enterprise app will need to work out?
- Authentication

This repository contains a [StereoKit](https://stereokit.net/) sample application for UWP devices (such as the Microsoft HoloLens 2) 
that demonstrates user authentication using Microsoft Azure Active Directory and MSAL.

![UI Image](docs/UI_loging_window_hl2.jpg)
![UI Image](docs/UI_hl2.jpg)

The application demonstrates the following:
* Authenticating users via MSAL token acquisition before they can use the StereoKit application.
* Acquires the access token to upload a blob to Azure Blob Storage. Then it downloads and displays the content.
* Demonstrates both silent and device-code-flow token acquisition.

## Azure Setup:
Please look at the following samples to learn the necessary setup in the Azure Portal:  
- https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-app?tabs=dotnet
- https://learn.microsoft.com/en-us/azure/active-directory/develop/tutorial-v2-windows-uwp

## Usage
In the code, edit both the Client ID and Tenant ID according to your application registration credentials. 
Also, edit the storage account URL according to you Azure Blob Storage setup.

## Contribution
We made the code open-source so that other StereoKit developers can also start using them in building their 
enterprise applications.
Some of the next features we are intersted in are:
* Instead of typing in user-name and password, we are more intestesd in eye-scan + AAD based authentication.
* Having a sample that demostrates upload of blob data to different storage accounts depending on which tenant the logged in
user is from. e.g. If someone logs in from Company A, he/she will be automatically be uploading to a Azure Storage
Account dedicated for that Company A. Similarly, if a user logs in from Company B, then the link to the blob 
storage acccount automatically updates. Currently, this storage account link is hardcoded into the program.

## Package Versioning
UWP is a dying platform, so it makes sense that Microsoft.Identity.Client [discontinued support for UWP](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/4427#issuecomment-2119973049). Thus, we require the latest non-vulnerable package version: <b>4.47.2</b>. For non-UWP applications, most of the code is similar, except you'll need to make minor changes to the broker options for token acquisition.
