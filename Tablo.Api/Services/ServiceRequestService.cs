using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class ServiceRequestService : IServiceRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public ServiceRequestService(ApplicationDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<ServiceRequestDto> CreateRequestAsync(string qrToken, CreateServiceRequestDto requestDto)
        {
            var table = await _context.RestaurantTables
                .Include(t => t.Restaurant)
                .FirstOrDefaultAsync(t => t.QRToken == qrToken && t.IsActive);

            if (table == null)
            {
                throw new Exception("Invalid or inactive QR token.");
            }

            if (requestDto.Type != "CallWaiter" && requestDto.Type != "RequestBill")
            {
                throw new Exception("Invalid service request type.");
            }

            // Check if there's already a pending request of this type for this table
            var existingPending = await _context.ServiceRequests
                .FirstOrDefaultAsync(sr => sr.TableId == table.Id && sr.Type == requestDto.Type && sr.Status == "Pending");

            if (existingPending != null)
            {
                throw new Exception($"A {requestDto.Type} request is already pending for this table.");
            }

            var serviceRequest = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                Type = requestDto.Type,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ServiceRequests.Add(serviceRequest);
            await _context.SaveChangesAsync();

            var dto = MapToDto(serviceRequest, table);

            // Broadcast to the restaurant staff group
            await _hubContext.Clients.Group($"restaurant_{table.RestaurantId}").SendAsync("NewServiceRequest", dto);

            return dto;
        }

        public async Task<IEnumerable<ServiceRequestDto>> GetActiveRequestsAsync(Guid restaurantId)
        {
            var activeStatuses = new[] { "Pending", "Acknowledged" };

            var requests = await _context.ServiceRequests
                .Include(sr => sr.Table)
                .Where(sr => sr.RestaurantId == restaurantId && activeStatuses.Contains(sr.Status))
                .OrderBy(sr => sr.CreatedAt)
                .ToListAsync();

            return requests.Select(sr => MapToDto(sr, sr.Table));
        }

        public async Task<ServiceRequestDto> AcknowledgeRequestAsync(Guid restaurantId, Guid requestId)
        {
            return await UpdateStatusAsync(restaurantId, requestId, "Acknowledged");
        }

        public async Task<ServiceRequestDto> CompleteRequestAsync(Guid restaurantId, Guid requestId)
        {
            return await UpdateStatusAsync(restaurantId, requestId, "Completed");
        }

        private async Task<ServiceRequestDto> UpdateStatusAsync(Guid restaurantId, Guid requestId, string newStatus)
        {
            var serviceRequest = await _context.ServiceRequests
                .Include(sr => sr.Table)
                .FirstOrDefaultAsync(sr => sr.Id == requestId && sr.RestaurantId == restaurantId);

            if (serviceRequest == null)
            {
                throw new Exception("Service request not found or unauthorized.");
            }

            // Simple state transition validation
            if (serviceRequest.Status == "Completed" || serviceRequest.Status == "Cancelled")
            {
                throw new Exception($"Cannot change status of a {serviceRequest.Status.ToLower()} request.");
            }

            serviceRequest.Status = newStatus;
            serviceRequest.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var dto = MapToDto(serviceRequest, serviceRequest.Table);

            // Broadcast status update
            await _hubContext.Clients.Group($"restaurant_{restaurantId}").SendAsync("ServiceRequestUpdated", dto);

            return dto;
        }

        private ServiceRequestDto MapToDto(ServiceRequest sr, RestaurantTable table)
        {
            return new ServiceRequestDto
            {
                Id = sr.Id,
                RestaurantId = sr.RestaurantId,
                TableId = sr.TableId,
                TableName = table != null ? $"Table {table.TableNumber}" : string.Empty,
                Type = sr.Type,
                Status = sr.Status,
                CreatedAt = sr.CreatedAt,
                UpdatedAt = sr.UpdatedAt
            };
        }
    }
}
