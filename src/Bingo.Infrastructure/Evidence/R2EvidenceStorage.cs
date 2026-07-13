using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Bingo.Application.Evidence;
using Microsoft.Extensions.Configuration;

namespace Bingo.Infrastructure.Evidence;

public sealed class R2EvidenceStorage : IEvidenceStorage, IDisposable
{
    private readonly AmazonS3Client client;
    private readonly string bucket;

    public R2EvidenceStorage(IConfiguration configuration)
    {
        var section = configuration.GetSection("EvidenceStorage:R2");
        var accountId = Required(section["AccountId"], "EvidenceStorage:R2:AccountId");
        var accessKeyId = Required(section["AccessKeyId"], "EvidenceStorage:R2:AccessKeyId");
        var secretAccessKey = Required(section["SecretAccessKey"], "EvidenceStorage:R2:SecretAccessKey");
        bucket = Required(section["Bucket"], "EvidenceStorage:R2:Bucket");
        var config = new AmazonS3Config
        {
            ServiceURL = section["ServiceUrl"] ?? $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };
        client = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
    }

    public async Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
    {
        await using var evidence = await EvidenceImageValidator.ReadAsync(content, cancellationToken);
        var key = $"{eventId:N}/{submissionId:N}/{Guid.NewGuid():N}{evidence.Extension}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = evidence.Content,
            ContentType = evidence.MediaType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        }, cancellationToken);
        return new StoredEvidence(key, EvidenceImageValidator.SafeFilename(originalFilename, evidence.Extension), evidence.MediaType, evidence.ByteSize, evidence.Width, evidence.Height, evidence.Checksum);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetObjectAsync(bucket, storageKey, cancellationToken);
        var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return copy;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => client.DeleteObjectAsync(bucket, storageKey, cancellationToken);
    public void Dispose() => client.Dispose();
    private static string Required(string? value, string key) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"The required setting '{key}' is missing.") : value;
}
