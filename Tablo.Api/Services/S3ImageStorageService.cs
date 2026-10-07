using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Tablo.Api.Configurations;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class S3ImageStorageService : IImageStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ImageStorageOptions _options;

        public S3ImageStorageService(IAmazonS3 s3Client, IOptions<ImageStorageOptions> options)
        {
            _s3Client = s3Client;
            _options = options.Value;
        }

        private string GetObjectKey(string imagePath)
        {
            return imagePath.TrimStart('/');
        }

        public async Task<string> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_options.S3.BucketName))
                throw new InvalidOperationException("S3 BucketName is not configured.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = Guid.NewGuid().ToString() + ext;
            var objectKey = $"images/menu/{uniqueFileName}";

            using var stream = file.OpenReadStream();
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.S3.BucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = file.ContentType
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            return $"/{objectKey}";
        }

        public async Task DeleteImageAsync(string? imagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(_options.S3.BucketName)) 
                return;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.S3.BucketName,
                Key = GetObjectKey(imagePath)
            };

            try
            {
                await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            }
            catch (AmazonS3Exception)
            {
                // Ignore missing file errors as soft deletes or missing objects are acceptable during deletion
            }
        }

        public string? GetImageUrl(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return imagePath;
            if (imagePath.StartsWith("http")) return imagePath; // already full URL or presigned
            if (string.IsNullOrEmpty(_options.S3.BucketName)) return imagePath; // fallback

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.S3.BucketName,
                Key = GetObjectKey(imagePath),
                Expires = DateTime.UtcNow.AddDays(7)
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}
