namespace Tablo.Api.Services;

public static class UnitConverter
{
    public static readonly string[] AllowedBaseUnits = ["g", "ml", "pieces"];

    public static string CanonicalizeBaseUnit(string baseUnit)
    {
        if (string.IsNullOrWhiteSpace(baseUnit))
            throw new ArgumentException("Base unit cannot be empty.");

        var normalized = baseUnit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "g" or "gram" or "grams" => "g",
            "ml" or "milliliter" or "milliliters" => "ml",
            "pieces" or "piece" or "pcs" or "pc" or "unit" or "units" => "pieces",
            _ => throw new ArgumentException($"Invalid base unit '{baseUnit}'. Allowed base units are 'g', 'ml', or 'pieces'.")
        };
    }

    /// <summary>
    /// Converts a quantity from an input unit to the target base unit.
    /// Rejects incompatible or cross-family conversions (e.g. weight to volume, pieces to weight).
    /// </summary>
    public static decimal NormalizeToBase(decimal quantity, string inputUnit, string baseUnit)
    {
        var targetBase = CanonicalizeBaseUnit(baseUnit);
        if (string.IsNullOrWhiteSpace(inputUnit))
            inputUnit = targetBase;

        var sourceUnit = inputUnit.Trim().ToLowerInvariant();

        switch (targetBase)
        {
            case "g":
                if (sourceUnit is "g" or "gram" or "grams")
                    return quantity;
                if (sourceUnit is "kg" or "kilogram" or "kilograms")
                    return quantity * 1000m;
                throw new ArgumentException($"Incompatible unit conversion: cannot convert '{inputUnit}' to weight base unit 'g'.");

            case "ml":
                if (sourceUnit is "ml" or "milliliter" or "milliliters")
                    return quantity;
                if (sourceUnit is "l" or "liter" or "liters" or "litre" or "litres")
                    return quantity * 1000m;
                throw new ArgumentException($"Incompatible unit conversion: cannot convert '{inputUnit}' to volume base unit 'ml'.");

            case "pieces":
                if (sourceUnit is "pieces" or "piece" or "pcs" or "pc" or "unit" or "units")
                    return quantity;
                throw new ArgumentException($"Incompatible unit conversion: cannot convert '{inputUnit}' to count base unit 'pieces'.");

            default:
                throw new ArgumentException($"Unsupported base unit '{baseUnit}'.");
        }
    }

    /// <summary>
    /// Calculates raw stock quantity consumed given usable quantity and yield percentage.
    /// Formula: UsableQuantity / (YieldPercent / 100)
    /// Example: 80g usable at 80% yield = 80 / 0.8 = 100g raw stock consumed.
    /// </summary>
    public static decimal CalculateRawQuantity(decimal usableQuantity, decimal yieldPercent)
    {
        if (yieldPercent <= 0)
            yieldPercent = 100m;

        return usableQuantity / (yieldPercent / 100m);
    }
}
