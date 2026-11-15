namespace Ecommerce.Api.Contracts;

public sealed record RegisterRequest(string Name, string Email, string Password, string? Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record CartRequest(int ProductId, int Quantity, string? Variant);
public sealed record WishlistRequest(int ProductId);
public sealed record ReviewRequest(int Rating, string? Title, string? Comment);
public sealed record AddressRequest(string Name, string Phone, string? AlternatePhone, string Pincode, string State, string City, string Locality, string HouseAddress, string? Landmark, string Type, bool IsDefault);
public sealed record CouponRequest(string Code, decimal Subtotal);
public sealed record CheckoutRequest(int AddressId, string PaymentMethod, string? CouponCode, string? PaymentReference, string? Notes);
public sealed record ContactRequest(string Name, string Email, string Subject, string? OrderNumber, string Message);
public sealed record JobApplicationRequest(string Name, string Email, string? Phone, string? Note, string? ResumeUrl);
public sealed record StatusRequest(string Status, string? Notes, string? TrackingNumber, string? Courier);
