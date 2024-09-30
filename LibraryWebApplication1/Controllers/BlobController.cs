using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LibraryWebApplication1.Controllers
{
    public class BlobController : Controller
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _blobContainerName = "deniscontainers"; // замініть на ваше значення

        public BlobController(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please select a file to upload.");
            }

            try
            {
                // Отримуємо доступ до контейнера
                var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);

                // Перевіряємо чи існує контейнер, якщо ні - створюємо його
                await containerClient.CreateIfNotExistsAsync();

                // Генеруємо унікальне ім'я для файлу
                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

                // Завантажуємо файл у Blob Storage
                var blobClient = containerClient.GetBlobClient(fileName);
                using (var stream = file.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream);
                }

                return Ok($"File uploaded successfully: {fileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult UploadForm()
        {
            return View();
        }
    }
}