using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs;

// ==========================================
// 1. MODIFIER DTOs
// ==========================================

public class SelectedModifierOptionRequestDto
{
    [Required]
    public Guid ModifierGroupId { get; set; }

    [Required]
    public Guid ModifierOptionId { get; set; }
}

public class SelectedModifierSnapshotDto
{
    public Guid ModifierGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid ModifierOptionId { get; set; }
    public string OptionName { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public string IngredientAdjustmentType { get; set; } = "None"; // None, Add, Substitute, Remove
    public Guid? IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal IngredientQuantity { get; set; }
    public Guid? OriginalIngredientId { get; set; }
    public string? OriginalIngredientName { get; set; }
}

public class CreateModifierGroupDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int MinSelections { get; set; } = 0;
    public int MaxSelections { get; set; } = 1;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public List<CreateModifierOptionDto>? Options { get; set; }
}

public class UpdateModifierGroupDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int MinSelections { get; set; } = 0;
    public int MaxSelections { get; set; } = 1;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class CreateModifierOptionDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; } = 0m;
    public int DisplayOrder { get; set; } = 0;
    public bool IsAvailable { get; set; } = true;
    public string IngredientAdjustmentType { get; set; } = "None";
    public Guid? IngredientId { get; set; }
    public decimal IngredientQuantity { get; set; } = 0m;
    public Guid? OriginalIngredientId { get; set; }
}

public class UpdateModifierOptionDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; } = 0m;
    public int DisplayOrder { get; set; } = 0;
    public bool IsAvailable { get; set; } = true;
    public string IngredientAdjustmentType { get; set; } = "None";
    public Guid? IngredientId { get; set; }
    public decimal IngredientQuantity { get; set; } = 0m;
    public Guid? OriginalIngredientId { get; set; }
}

public class ModifierOptionDto
{
    public Guid Id { get; set; }
    public Guid ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsAvailable { get; set; }
    public string IngredientAdjustmentType { get; set; } = "None";
    public Guid? IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal IngredientQuantity { get; set; }
    public Guid? OriginalIngredientId { get; set; }
    public string? OriginalIngredientName { get; set; }
}

public class ModifierGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelections { get; set; }
    public int MaxSelections { get; set; }
    public bool IsRequired => MinSelections > 0;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public List<ModifierOptionDto> Options { get; set; } = new();
}

public class AssignModifierGroupsRequestDto
{
    public List<Guid> ModifierGroupIds { get; set; } = new();
}

// ==========================================
// 2. HOLD & FIRE DTOs
// ==========================================

public class FireOrderItemsRequestDto
{
    [Required]
    public string OperationId { get; set; } = string.Empty;

    public List<Guid>? OrderItemIds { get; set; }
    public string? CourseName { get; set; }
    public Guid? FiredByEmployeeId { get; set; }
}

public class FiredItemsResponseDto
{
    public Guid OrderId { get; set; }
    public int FiredItemCount { get; set; }
    public List<Guid> FiredOrderItemIds { get; set; } = new();
    public int GeneratedKotTicketsCount { get; set; }
    public List<string> KotTicketNumbers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

// ==========================================
// 3. REFILL DTOs
// ==========================================

public class CreateRefillRequestDto
{
    [Required]
    public string OperationId { get; set; } = string.Empty;

    [Range(1, 10, ErrorMessage = "Refill quantity must be between 1 and 10")]
    public int Quantity { get; set; } = 1;

    public Guid? ActorEmployeeId { get; set; }

    [MaxLength(255)]
    public string? Notes { get; set; }
}

public class RefillResponseDto
{
    public Guid OrderId { get; set; }
    public Guid RefillOrderItemId { get; set; }
    public Guid OriginalOrderItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int RefillCount { get; set; }
    public decimal PriceCharged { get; set; } = 0.00m;
    public string KotTicketNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// ==========================================
// 4. MANAGER-APPROVED CANCELLATION DTOs
// ==========================================

public class CancelOrderItemRequestDto
{
    [Required]
    public string OperationId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Cancel quantity must be at least 1")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// "NotCooked" or "KitchenWaste"
    /// </summary>
    [Required]
    public string Outcome { get; set; } = "NotCooked";

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public string? ManagerPin { get; set; }
    public Guid? RequestedByEmployeeId { get; set; }
    public Guid? ApprovedByEmployeeId { get; set; }
}

public class ItemCancellationResponseDto
{
    public Guid CancellationLogId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int QuantityCancelled { get; set; }
    public int RemainingQuantity { get; set; }
    public string ProductionOutcome { get; set; } = string.Empty;
    public decimal NewOrderTotal { get; set; }
    public decimal? WastedCost { get; set; }
    public string? CancellationKotNoticeNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SetManagerPinRequestDto
{
    [Required]
    [RegularExpression(@"^\d{4,8}$", ErrorMessage = "Manager PIN must be between 4 and 8 digits.")]
    public string Pin { get; set; } = string.Empty;
}

public class VerifyManagerPinRequestDto
{
    [Required]
    public string Pin { get; set; } = string.Empty;
}
