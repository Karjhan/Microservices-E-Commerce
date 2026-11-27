namespace Application.DTOs;

public record UploadProductImageDto(
    Guid ImageId,
    string Url,
    bool IsPrimary = false,
    int SortOrder = 0,
    string ContentType = "",
    long SizeBytes = 0);
