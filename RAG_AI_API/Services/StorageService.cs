using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace RAG_AI_API.Services;


public interface IStorageService
{
    Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task<byte[]> ReadFileAsync(string key, CancellationToken cancellationToken = default);

    Task<GetObjectResponse> GetObjectAsync(string key, CancellationToken cancellationToken = default);
}


public class StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _storageOptions;

    public StorageService(IAmazonS3 s3Client, IOptions<StorageOptions> storageOptions)
    {
        _s3Client = s3Client;
        _storageOptions = storageOptions.Value;
    }


    public async Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty.");

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));

        var key = $"uploads/{Guid.CreateVersion7(DateTime.UtcNow):N}{extension}";

        await using var inputStream = file.OpenReadStream();

        var dd = _storageOptions.BucketName;

        var request = new PutObjectRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = key,
            InputStream = inputStream,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        return key;
    }


    public async Task<byte[]> ReadFileAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _s3Client.GetObjectAsync(_storageOptions.BucketName, key, cancellationToken);

        await using var memoryStream = new MemoryStream();

        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);

        return memoryStream.ToArray();
    }


    public Task<GetObjectResponse> GetObjectAsync(string key,  CancellationToken cancellationToken = default)
    {
        return _s3Client.GetObjectAsync(_storageOptions.BucketName, key, cancellationToken);
    }
}
