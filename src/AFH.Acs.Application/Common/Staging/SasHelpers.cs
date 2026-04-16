using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace AFH.Acs.Recorder.Helpers;


public static class SasHelpers
{

    // Generate one SAS for the container
    public static string GenerateSasUrl(BlobContainerClient containerClient,
                                      BlobContainerSasPermissions permissions = BlobContainerSasPermissions.Read,
                                      int expiryMinutes = 60)
    {
        // Ensure the client has a Shared Key credential
        if (!containerClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("BlobContainerClient must be created with a StorageSharedKeyCredential to generate SAS.");
        }

        // Build SAS options
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerClient.Name,
            Resource = "c", // "c" = container
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        };

        sasBuilder.SetPermissions(permissions);

        // Generate the SAS URI
        Uri sasUri = containerClient.GenerateSasUri(sasBuilder);

        return sasUri.ToString();
    }


    public static string GenerateSasUrl(BlobClient blob)
    {
        if (!blob.CanGenerateSasUri)
        {
            // Fallback – public / account SAS / dev storage
            return blob.Uri.ToString();
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blob.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }


}
