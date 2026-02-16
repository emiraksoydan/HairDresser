using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Core.Utilities.Storage
{
    /// <summary>
    /// Sunucu yerel diskinde dosya saklar.
    /// Dosyalar LocalStorage:UploadRoot/{containerName}/ altında saklanır.
    /// URL formatı: {LocalStorage:BaseUrl}/uploads/{containerName}/{fileName}
    ///
    /// appsettings.json:
    ///   "LocalStorage": {
    ///     "BaseUrl": "https://api.gumusmakas.com.tr",
    ///     "UploadRoot": "/var/app/wwwroot/uploads"   (veya Windows: "C:\\inetpub\\wwwroot\\uploads")
    ///   }
    ///
    /// Not: Tek sunucu için uygundur. Sunucu değişirse dosyaların yedeklenmesi gerekir.
    /// </summary>
    public class LocalFileStorageService : IBlobStorageService
    {
        private readonly string _baseUrl;
        private readonly string _uploadRoot;

        public LocalFileStorageService(IConfiguration configuration)
        {
            _baseUrl = (configuration["LocalStorage:BaseUrl"] ?? "").TrimEnd('/');
            _uploadRoot = configuration["LocalStorage:UploadRoot"] ?? "wwwroot/uploads";
        }

        public async Task<string> UploadAsync(IFormFile file, string containerName, string? fileName = null)
        {
            var dir = Path.Combine(_uploadRoot, containerName);
            Directory.CreateDirectory(dir);

            var ext = Path.GetExtension(file.FileName);
            fileName ??= $"{Guid.NewGuid()}{ext}";

            var filePath = Path.Combine(dir, fileName);
            await using var stream = File.Create(filePath);
            await file.CopyToAsync(stream);

            return $"{_baseUrl}/uploads/{containerName}/{fileName}";
        }

        public async Task<List<string>> UploadMultipleAsync(List<IFormFile> files, string containerName)
        {
            var urls = new List<string>();
            foreach (var file in files)
                urls.Add(await UploadAsync(file, containerName));
            return urls;
        }

        public Task<bool> DeleteAsync(string fileUrl)
        {
            try
            {
                var filePath = UrlToFilePath(fileUrl);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task<bool> DeleteMultipleAsync(List<string> fileUrls)
        {
            foreach (var url in fileUrls)
                await DeleteAsync(url);
            return true;
        }

        public Task<bool> ExistsAsync(string fileUrl)
        {
            var filePath = UrlToFilePath(fileUrl);
            return Task.FromResult(File.Exists(filePath));
        }

        public async Task<string> UpdateAsync(IFormFile file, string existingFileUrl)
        {
            // Eski dosyanın üzerine yaz (aynı URL korunur, ImageManager timestamp ekler)
            var cleanUrl = existingFileUrl.Split('?')[0];
            var filePath = UrlToFilePath(cleanUrl);
            var dir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(dir);

            await using var stream = File.Create(filePath);
            await file.CopyToAsync(stream);

            return cleanUrl;
        }

        private string UrlToFilePath(string fileUrl)
        {
            // URL: https://api.gumusmakas.com.tr/uploads/user-images/uuid.jpg?t=xxx
            // FilePath: /var/app/wwwroot/uploads/user-images/uuid.jpg
            var cleanUrl = fileUrl.Split('?')[0];
            var uri = new Uri(cleanUrl);
            var relativePath = uri.AbsolutePath.TrimStart('/'); // uploads/user-images/uuid.jpg
            var afterUploads = relativePath.StartsWith("uploads/")
                ? relativePath.Substring("uploads/".Length)
                : relativePath;
            return Path.Combine(_uploadRoot, afterUploads.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
