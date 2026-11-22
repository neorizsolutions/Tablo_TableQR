using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services.Printing;

public interface IKotRoutingService
{
    Task<List<KotTicket>> ProcessAcceptedOrderItemsAsync(
        Order order,
        List<OrderItem> newlyAcceptedItems,
        string actorEmail = "Staff",
        string trigger = "StaffAccepted");

    Task<KotTicket> ReprintTicketAsync(
        Guid restaurantId,
        Guid ticketId,
        string actorEmail,
        string reason);

    Task RecordTableTransferNoticeAsync(
        Guid restaurantId,
        Guid orderId,
        int oldTable,
        int newTable,
        string? oldGroup,
        string? newGroup);

    Task RecordOrderCancellationNoticeAsync(
        Guid restaurantId,
        Guid orderId,
        string reason);

    Task<KotTicket?> RecordItemCancellationNoticeAsync(
        Guid restaurantId,
        Guid orderId,
        OrderItem item,
        int cancelledQuantity,
        string outcome,
        string reason,
        string? approverName);

    Task<RoutingPreviewResponseDto> PreviewRoutingAsync(
        Guid restaurantId,
        List<RoutingPreviewItemRequestDto> items);
}
