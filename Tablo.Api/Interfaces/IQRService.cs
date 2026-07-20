using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IQRService
    {
        Task<QRResolutionDto?> ResolveQRTokenAsync(string qrToken);
    }
}
