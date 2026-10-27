namespace ORO_POS.Core.DTOs;

public class RefundRequest
{
    public string? ApproverUsername { get; set; }
    public string? ApproverPassword { get; set; }
    public string Reason { get; set; } = string.Empty;
    // Explicit choice: food/waste must not be automatically returned to stock.
    public bool Restock { get; set; }
    public string Reference { get; set; } = string.Empty;
}
