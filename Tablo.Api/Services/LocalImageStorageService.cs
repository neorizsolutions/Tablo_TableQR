using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class LocalImageStorageService : IImageStorageService
    {
        public async Task<string> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "menu");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + ext;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream, cancellationToken);
            }

            return $"/images/menu/{uniqueFileName}";
        }

        public Task DeleteImageAsync(string? imagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imagePath)) return Task.CompletedTask;
            
            var relativePath = imagePath.TrimStart('/');
            // prevent directory traversal
            if (relativePath.Contains("..")) return Task.CompletedTask;

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
            
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
            return Task.CompletedTask;
        }

        public string? GetImageUrl(string? imagePath)
        {
            return imagePath;
        }
    }
}
