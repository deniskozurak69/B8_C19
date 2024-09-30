using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Threading.Tasks;

public class AzureBlobService
{
    private readonly string _connectionString;
    private readonly string _containerName;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AzureBlobService(string connectionString, string containerName, IWebHostEnvironment webHostEnvironment)
    {
        _connectionString = connectionString;
        _containerName = containerName;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<string> UploadFileToBlobAsync(string filePath)
    {
        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
        string fileName = Path.GetFileName(filePath);
        string absoluteFilePath = Path.Combine(uploadsFolder, fileName);
        BlobServiceClient blobServiceClient = new BlobServiceClient(_connectionString);
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient(fileName);

        using (FileStream fs = File.OpenRead(absoluteFilePath))
        {
            await blobClient.UploadAsync(fs, true);
        }

        return blobClient.Uri.ToString();
    }
}