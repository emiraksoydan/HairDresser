using Microsoft.AspNetCore.Http;

namespace Core.Utilities.Storage
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a file to Azure Blob Storage
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="containerName">Container name (e.g., "user-images", "store-images")</param>
        /// <param name="fileName">Optional custom file name. If null, generates a unique name</param>
        /// <returns>The URL of the uploaded file</returns>
        Task<string> UploadAsync(IFormFile file, string containerName, string? fileName = null);

        /// <summary>
        /// Uploads multiple files to Azure Blob Storage
        /// </summary>
        Task<List<string>> UploadMultipleAsync(List<IFormFile> files, string containerName);

        /// <summary>
        /// Deletes a file from Azure Blob Storage
        /// </summary>
        /// <param name="fileUrl">The full URL of the file to delete</param>
        Task<bool> DeleteAsync(string fileUrl);

        /// <summary>
        /// Deletes multiple files from Azure Blob Storage
        /// </summary>
        Task<bool> DeleteMultipleAsync(List<string> fileUrls);

        /// <summary>
        /// Checks if a file exists in Azure Blob Storage
        /// </summary>
        Task<bool> ExistsAsync(string fileUrl);
    }
}
