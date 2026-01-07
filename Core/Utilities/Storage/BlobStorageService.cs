
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Core.Utilities.Storage
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _storageAccountUrl;

        public BlobStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"];
            _storageAccountUrl = configuration["AzureBlobStorage:StorageAccountUrl"];
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public async Task<string> UploadAsync(IFormFile file, string containerName, string? fileName = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            // Container'ı al veya oluştur
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Dosya adı belirtilmemişse unique bir isim oluştur
            if (string.IsNullOrEmpty(fileName))
            {
                var extension = Path.GetExtension(file.FileName);
                
                fileName = $"{Guid.NewGuid()}{extension}";
            }

            // Blob client oluştur
            var blobClient = containerClient.GetBlobClient(fileName);

            // Dosyayı yükle
            using (var stream = file.OpenReadStream())
            {
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    }
                };
                await blobClient.UploadAsync(stream, uploadOptions);
            }

            // URL'i döndür
            return blobClient.Uri.ToString();
        }

        public async Task<List<string>> UploadMultipleAsync(List<IFormFile> files, string containerName)
        {
            var urls = new List<string>();

            foreach (var file in files)
            {
                var url = await UploadAsync(file, containerName);
                urls.Add(url);
            }

            return urls;
        }

        public async Task<bool> DeleteAsync(string fileUrl)
        {
            try
            {
                // URL'den container ve blob adını ayıkla
                var uri = new Uri(fileUrl);
                var segments = uri.Segments;

                if (segments.Length < 2)
                    return false;

                var containerName = segments[1].TrimEnd('/');
                var blobName = string.Join("", segments.Skip(2));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                return await blobClient.DeleteIfExistsAsync();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteMultipleAsync(List<string> fileUrls)
        {
            var results = new List<bool>();

            foreach (var url in fileUrls)
            {
                var result = await DeleteAsync(url);
                results.Add(result);
            }

            // Tüm silme işlemleri başarılıysa true döndür
            return results.All(r => r);
        }

        public async Task<bool> ExistsAsync(string fileUrl)
        {
            try
            {
                // URL'den container ve blob adını ayıkla
                var uri = new Uri(fileUrl);
                var segments = uri.Segments;

                if (segments.Length < 2)
                    return false;

                var containerName = segments[1].TrimEnd('/');
                var blobName = string.Join("", segments.Skip(2));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                return await blobClient.ExistsAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates an existing blob with new file content without creating a new blob
        /// </summary>
        public async Task<string> UpdateAsync(IFormFile file, string existingFileUrl)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            if (string.IsNullOrEmpty(existingFileUrl))
                throw new ArgumentException("Existing file URL is required", nameof(existingFileUrl));

            try
            {
                // URL'den container ve blob adını ayıkla
                var uri = new Uri(existingFileUrl);
                var segments = uri.Segments;

                if (segments.Length < 2)
                    throw new ArgumentException("Invalid file URL", nameof(existingFileUrl));

                var containerName = segments[1].TrimEnd('/');
                var blobName = string.Join("", segments.Skip(2));

                // Container'ı al (oluşturma gerekmez çünkü zaten var olmalı)
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                
                // Mevcut blob'u al
                var blobClient = containerClient.GetBlobClient(blobName);

                // Dosyayı mevcut blob üzerine yaz (overwrite)
                using (var stream = file.OpenReadStream())
                {
                    var uploadOptions = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = file.ContentType
                        }
                    };
                    await blobClient.UploadAsync(stream, uploadOptions);
                }

                // Aynı URL'i döndür (blob name değişmediği için)
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update blob: {ex.Message}", ex);
            }
        }
    }
}
