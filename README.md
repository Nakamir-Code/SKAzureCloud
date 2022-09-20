# AzureStereoKitUWPSamples
In this repo, we demostrate usage of Microsoft Azure Active Directory (AAD) based authentication of a user from a UWP [StereoKit](https://stereokit.net/) application runnning on Microsoft Hololens-2.

![UI Image](UI_loging_window_hl2.jpg)
![UI Image](UI_hl2.jpg)

The application has demonstrations of:
* Token acquisition using MSAL in a StereoKit-UWP application running on Hololens-2.
* Using the token to get data from Microsoft Graph API.
* Using the token to upload a blob to azure blob storage and then read the contents of the blob.
* Both silent and interactive token acquisition is demonstrated.

## Azure Setup:
Please look at the following samples to learn to do all the necessary setups using the Azure Portal.  
https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-app?tabs=dotnet

https://learn.microsoft.com/en-us/azure/active-directory/develop/tutorial-v2-windows-uwp

## Instruction:
In the code, edit the client Id and tenant Id according to your application registration credentials. Also, edit the storage account URL according to you Azure Blob
Storage setup.

