using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs;

public class KitchenStationDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid? OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public Guid? PrinterId { get; set; }
    public string? PrinterName { get; set; }
    public int DisplayOrder { get; set; }
    public int AssignedCategoryCount { get; set; }
    public int AssignedMenuItemCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateKitchenStationDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; } = false;

    public Guid? PrinterId { get; set; }

    public int DisplayOrder { get; set; } = 0;
}

public class UpdateKitchenStationDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public Guid? PrinterId { get; set; }

    public int DisplayOrder { get; set; }
}

public class ThermalPrinterDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid? OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "NetworkTcp"; // NetworkTcp or LocalAgentQueue
    public string? IpAddress { get; set; }
    public int Port { get; set; }
    public string? LocalQueueName { get; set; }
    public int PaperWidthMm { get; set; }
    public int CharactersPerLine { get; set; }
    public string Status { get; set; } = "Unknown";
    public DateTime? LastSeenAtUtc { get; set; }
    public string? LastStatusMessage { get; set; }
    public bool IsActive { get; set; }
    public List<string> AssignedStationNames { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateThermalPrinterDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ConnectionType { get; set; } = "NetworkTcp";

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    public int Port { get; set; } = 9100;

    [MaxLength(100)]
    public string? LocalQueueName { get; set; }

    [Range(58, 80)]
    public int PaperWidthMm { get; set; } = 80;

    public int CharactersPerLine { get; set; } = 48;
}

public class UpdateThermalPrinterDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ConnectionType { get; set; } = "NetworkTcp";

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    public int Port { get; set; } = 9100;

    [MaxLength(100)]
    public string? LocalQueueName { get; set; }

    [Range(58, 80)]
    public int PaperWidthMm { get; set; } = 80;

    public int CharactersPerLine { get; set; } = 48;

    public bool IsActive { get; set; } = true;
}

public class RegisterPrintAgentDto
{
    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;
}

public class PrintAgentRegisteredResultDto
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string PlaintextApiKey { get; set; } = string.Empty; // Only shown ONCE upon registration
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class PrintAgentDto
{
    public Guid Id { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public string? Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class KotTicketItemSnapshotDto
{
    public Guid OrderItemId { get; set; }
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? ModifiersJson { get; set; }
    public string? CourseName { get; set; }
    public string PackingIntent { get; set; } = "DineIn";
    public bool IsRefill { get; set; }
}

public class KotTicketDto
{
    public Guid Id { get; set; }
    public Guid KotBatchId { get; set; }
    public Guid OrderId { get; set; }
    public int OrderNumber { get; set; }
    public int TableNumber { get; set; }
    public string? GroupLabel { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public string TimestampIst { get; set; } = string.Empty;
    public bool IsReprint { get; set; }
    public int ReprintCount { get; set; }
    public List<KotTicketItemSnapshotDto> Items { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class PrintJobDto
{
    public Guid Id { get; set; }
    public Guid? KotTicketId { get; set; }
    public string? TicketNumber { get; set; }
    public Guid PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PaperWidthMm { get; set; }
    public int Attempts { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public string? LastError { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = "Certain";
    public string? UncertainReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string ReadableText { get; set; } = string.Empty;
}

public class RoutingPreviewRequestDto
{
    public List<RoutingPreviewItemRequestDto> Items { get; set; } = new();
}

public class RoutingPreviewItemRequestDto
{
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class RoutingPreviewResponseDto
{
    public string KitchenSetupMode { get; set; } = string.Empty;
    public List<StationRoutingPreviewDto> StationTickets { get; set; } = new();
}

public class StationRoutingPreviewDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string? AssignedPrinterName { get; set; }
    public List<StationRoutingPreviewItemDto> Items { get; set; } = new();
}

public class StationRoutingPreviewItemDto
{
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ResolvedBy { get; set; } = string.Empty; // "ItemOverride", "CategoryDefault", "DefaultStation"
}

public class ConsolidateStationsRequestDto
{
    public Guid TargetDefaultStationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ConsolidateStationsResultDto
{
    public Guid DefaultStationId { get; set; }
    public string DefaultStationName { get; set; } = string.Empty;
    public int DeactivatedStationsCount { get; set; }
    public int ReassignedCategoriesCount { get; set; }
    public int ReassignedMenuItemsCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReprintTicketRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public class AgentPollRequestDto
{
    public string? AgentVersion { get; set; }
}

public class AgentPollJobDto
{
    public Guid JobId { get; set; }
    public string ClaimToken { get; set; } = string.Empty;
    public Guid PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int Port { get; set; }
    public string? LocalQueueName { get; set; }
    public int PaperWidthMm { get; set; }
    public string PayloadEscPosBase64 { get; set; } = string.Empty;
    public string ReadableText { get; set; } = string.Empty;
}

public class AgentJobAckDto
{
    public Guid JobId { get; set; }
    [Required]
    public string ClaimToken { get; set; } = string.Empty;
    [Required]
    public string Status { get; set; } = "Completed"; // Completed, Printed, Failed, Uncertain, UncertainDelivery
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
}

public class AgentHeartbeatDto
{
    public string? AgentVersion { get; set; }
    public List<AgentPrinterStatusReportDto> PrinterStatuses { get; set; } = new();
}

public class AgentPrinterStatusReportDto
{
    public Guid PrinterId { get; set; }
    public string Status { get; set; } = "Online"; // Online, Offline, PaperOut, Error
    public string? StatusMessage { get; set; }
}
