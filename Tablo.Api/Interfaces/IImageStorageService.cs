using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Tablo.Api.Interfaces
{
    public interface IImageStorageService
    {
        Task<string> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task DeleteImageAsync(string? imagePath, CancellationToken cancellationToken = default);
        string? GetImageUrl(string? imagePath);
    }
}
