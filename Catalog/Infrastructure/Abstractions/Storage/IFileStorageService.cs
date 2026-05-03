namespace Application.Abstractions.Storage;

public interface IFileStorageService
{
    Task<string> UploadAsync(string filePath, string objectKey, string contentType);
    Task DeleteAsync(string objectKey);
}