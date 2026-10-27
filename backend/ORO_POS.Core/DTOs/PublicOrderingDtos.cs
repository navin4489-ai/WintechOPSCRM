using System.ComponentModel.DataAnnotations;
namespace ORO_POS.Core.DTOs;

public class GuestOrderRequest
{
    public Guid RequestId { get; set; }
    [Required, StringLength(80, MinimumLength = 1)] public string CustomerName { get; set; } = "";
    [Required, StringLength(24)] public string CustomerPhone { get; set; } = "";
    [StringLength(500)] public string? Notes { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}
public class PublicMenuDto
{
    public string OutletName { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Currency { get; set; } = "INR";
    public bool AcceptingOrders { get; set; }
    public string PaymentInstruction { get; set; } = "Pay cash at the outlet. No online payment is taken.";
    public List<ProductDto> Products { get; set; } = new();
}
public class GuestOrderConfirmation
{
    public string OrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TrackingProof { get; set; } = "";
    public string PaymentStatus { get; set; } = "Pay cash at the outlet";
}
public class OrderingSettingsRequest
{
    public bool Enabled { get; set; }
    public bool RotateKey { get; set; }
}
public class GuestTrackingRequest
{
    [Required, StringLength(64, MinimumLength = 64)] public string Proof { get; set; } = "";
}
