using Microsoft.AspNetCore.Http;

namespace Core.Utilities.Storage
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a file to storage
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="containerName">Container/folder name (e.g., "user-images", "store-images")</param>
        /// <param name="fileName">Optional custom file name. If null, generates a unique name</param>
        /// <returns>The URL of the uploaded file</returns>
        Task<string> UploadAsync(IFormFile file, string containerName, string? fileName = null);

        /// <summary>
        /// Uploads multiple files to storage
        /// </summary>
        Task<List<string>> UploadMultipleAsync(List<IFormFile> files, string containerName);

        /// <summary>
        /// Deletes a file from storage
        /// </summary>
        /// <param name="fileUrl">The full URL of the file to delete</param>
        Task<bool> DeleteAsync(string fileUrl);

        /// <summary>
        /// Deletes multiple files from storage
        /// </summary>
        Task<bool> DeleteMultipleAsync(List<string> fileUrls);

        /// <summary>
        /// Checks if a file exists in storage
        /// </summary>
        Task<bool> ExistsAsync(string fileUrl);

        /// <summary>
        /// Updates an existing file with new content without creating a new entry
        /// </summary>
        /// <param name="file">The new file content</param>
        /// <param name="existingFileUrl">The URL of the existing file to update</param>
        /// <returns>The URL of the updated file (same as existingFileUrl)</returns>
        Task<string> UpdateAsync(IFormFile file, string existingFileUrl);
    }
}
