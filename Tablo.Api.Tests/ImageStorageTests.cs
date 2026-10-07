using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Tablo.Api.Configurations;
using Tablo.Api.Services;
using Xunit;

namespace Tablo.Api.Tests
{
    public class ImageStorageTests
    {
        [Fact]
        public async Task LocalImageStorageService_Upload_ReturnsValidPath()
        {
            var service = new LocalImageStorageService();
            var mockFile = new Mock<IFormFile>();
            var content = "test image content";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;

            mockFile.Setup(f => f.FileName).Returns("test.png");
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, CancellationToken>((stream, token) =>
                    {
                        ms.CopyTo(stream);
                    })
                    .Returns(Task.CompletedTask);

            var path = await service.UploadImageAsync(mockFile.Object);

            Assert.StartsWith("/images/menu/", path);
            Assert.EndsWith(".png", path);
        }

        [Fact]
        public void LocalImageStorageService_GetImageUrl_ReturnsSamePath()
        {
            var service = new LocalImageStorageService();
            var url = service.GetImageUrl("/images/menu/test.png");
            Assert.Equal("/images/menu/test.png", url);
        }

        [Fact]
        public async Task S3ImageStorageService_Upload_UploadsAndReturnsKey()
        {
            var s3Mock = new Mock<IAmazonS3>();
            var options = Options.Create(new ImageStorageOptions { S3 = new S3Options { BucketName = "test-bucket" } });
            var service = new S3ImageStorageService(s3Mock.Object, options);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

            s3Mock.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PutObjectResponse());

            var path = await service.UploadImageAsync(mockFile.Object);

            Assert.StartsWith("/images/menu/", path);
            Assert.EndsWith(".jpg", path);

            s3Mock.Verify(s => s.PutObjectAsync(It.Is<PutObjectRequest>(r => r.BucketName == "test-bucket" && r.Key.StartsWith("images/menu/") && r.ContentType == "image/jpeg"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void S3ImageStorageService_GetImageUrl_ReturnsPresignedUrl()
        {
            var s3Mock = new Mock<IAmazonS3>();
            var options = Options.Create(new ImageStorageOptions { S3 = new S3Options { BucketName = "test-bucket" } });
            
            s3Mock.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                  .Returns("https://test-bucket.s3.amazonaws.com/images/menu/test.jpg?signature=xxx");
                  
            var service = new S3ImageStorageService(s3Mock.Object, options);

            var url = service.GetImageUrl("/images/menu/test.jpg");
            Assert.StartsWith("https://", url);
        }

        [Fact]
        public async Task S3ImageStorageService_Delete_SendsDeleteRequest()
        {
            var s3Mock = new Mock<IAmazonS3>();
            var options = Options.Create(new ImageStorageOptions { S3 = new S3Options { BucketName = "test-bucket" } });
            var service = new S3ImageStorageService(s3Mock.Object, options);

            s3Mock.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new DeleteObjectResponse());

            await service.DeleteImageAsync("/images/menu/test.jpg");

            s3Mock.Verify(s => s.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r => r.BucketName == "test-bucket" && r.Key == "images/menu/test.jpg"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task S3ImageStorageService_Upload_ThrowsIfNoBucket()
        {
            var s3Mock = new Mock<IAmazonS3>();
            var options = Options.Create(new ImageStorageOptions { S3 = new S3Options { BucketName = "" } });
            var service = new S3ImageStorageService(s3Mock.Object, options);
            var mockFile = new Mock<IFormFile>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadImageAsync(mockFile.Object));
        }
    }
}
