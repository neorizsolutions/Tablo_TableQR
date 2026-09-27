using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IServiceRequestService
    {
        Task<ServiceRequestDto> CreateRequestAsync(string qrToken, CreateServiceRequestDto requestDto);
        Task<IEnumerable<ServiceRequestDto>> GetActiveRequestsAsync(Guid restaurantId);
        Task<ServiceRequestDto> AcknowledgeRequestAsync(Guid restaurantId, Guid requestId);
        Task<ServiceRequestDto> CompleteRequestAsync(Guid restaurantId, Guid requestId);
    }
}
