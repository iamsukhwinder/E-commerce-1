using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ecommerce.Api.Domain;

public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class User : Entity
{
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(256)] public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    [MaxLength(20)] public string Role { get; set; } = "customer";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(500)] public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Address> Addresses { get; set; } = [];
}

public sealed class Category : Entity
{
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(140)] public required string Slug { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class Brand : Entity
{
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(140)] public required string Slug { get; set; }
    [MaxLength(500)] public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Product : Entity
{
    [MaxLength(180)] public required string Name { get; set; }
    [MaxLength(180)] public required string Slug { get; set; }
    [MaxLength(80)] public required string Sku { get; set; }
    public string Description { get; set; } = "";
    public string? ShortDescription { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    [MaxLength(500)] public string? ImageUrl { get; set; }
    public string? GalleryJson { get; set; }
    public string? DetailsJson { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int BrandId { get; set; }
    public Brand? Brand { get; set; }
    public ICollection<Review> Reviews { get; set; } = [];
}

public sealed class Review : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(140)] public string? Title { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }
    public bool IsApproved { get; set; } = true;
}

public sealed class CartItem : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    [MaxLength(100)] public string? Variant { get; set; }
}

public sealed class WishlistItem : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
}

public sealed class Address : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(30)] public required string Phone { get; set; }
    [MaxLength(30)] public string? AlternatePhone { get; set; }
    [MaxLength(20)] public required string Pincode { get; set; }
    [MaxLength(100)] public required string State { get; set; }
    [MaxLength(100)] public required string City { get; set; }
    [MaxLength(240)] public required string Locality { get; set; }
    [MaxLength(300)] public required string HouseAddress { get; set; }
    [MaxLength(200)] public string? Landmark { get; set; }
    [MaxLength(30)] public string Type { get; set; } = "Home";
    public bool IsDefault { get; set; }
}

public sealed class Coupon : Entity
{
    [MaxLength(40)] public required string Code { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? DiscountPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? DiscountAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MinimumOrderValue { get; set; }
    public string Description { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Order : Entity
{
    [MaxLength(30)] public required string OrderNumber { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int AddressId { get; set; }
    public Address? Address { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Pending";
    [MaxLength(40)] public string PaymentStatus { get; set; } = "Pending";
    [MaxLength(80)] public string PaymentMethod { get; set; } = "cod";
    [MaxLength(100)] public string? PaymentReference { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Subtotal { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Shipping { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Discount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Tax { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Total { get; set; }
    [MaxLength(80)] public string? TrackingNumber { get; set; }
    [MaxLength(120)] public string? Courier { get; set; }
    public string? Notes { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderEvent> Events { get; set; } = [];
}

public sealed class OrderItem : Entity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    [MaxLength(180)] public required string ProductName { get; set; }
    [MaxLength(80)] public required string Sku { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    [MaxLength(100)] public string? Variant { get; set; }
    [MaxLength(40)] public string ProcurementStatus { get; set; } = "None";
}

public sealed class OrderEvent : Entity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    [MaxLength(100)] public required string Title { get; set; }
    [MaxLength(120)] public required string Actor { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public sealed class Banner : Entity
{
    [MaxLength(80)] public required string Section { get; set; }
    [MaxLength(180)] public required string Title { get; set; }
    public string? Subtitle { get; set; }
    [MaxLength(500)] public required string ImageUrl { get; set; }
    [MaxLength(500)] public string? LinkUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class HomeSection : Entity
{
    [MaxLength(60)] public required string SectionKey { get; set; }
    [MaxLength(120)] public required string Type { get; set; }
    [MaxLength(180)] public required string Title { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPublished { get; set; } = true;
}

public sealed class BlogPost : Entity
{
    [MaxLength(180)] public required string Slug { get; set; }
    [MaxLength(220)] public required string Title { get; set; }
    [MaxLength(120)] public required string Category { get; set; }
    public string Excerpt { get; set; } = "";
    public string Content { get; set; } = "";
    [MaxLength(500)] public string? ImageUrl { get; set; }
    [MaxLength(120)] public required string AuthorName { get; set; }
    [MaxLength(120)] public string? AuthorRole { get; set; }
    public int ReadTimeMinutes { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; }
}

public sealed class JobOpening : Entity
{
    [MaxLength(180)] public required string Title { get; set; }
    [MaxLength(120)] public required string Department { get; set; }
    [MaxLength(160)] public required string Location { get; set; }
    [MaxLength(60)] public required string EmploymentType { get; set; }
    [MaxLength(80)] public string? Experience { get; set; }
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class JobApplication : Entity
{
    public int JobOpeningId { get; set; }
    public JobOpening? JobOpening { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(256)] public required string Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(500)] public string? ResumeUrl { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "New";
}

public sealed class ContactMessage : Entity
{
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(256)] public required string Email { get; set; }
    [MaxLength(160)] public required string Subject { get; set; }
    [MaxLength(40)] public string? OrderNumber { get; set; }
    [MaxLength(3000)] public required string Message { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "New";
}

public sealed class StoreSetting : Entity
{
    [MaxLength(120)] public required string Key { get; set; }
    public string Value { get; set; } = "";
}

public sealed class Notification : Entity
{
    public int? UserId { get; set; }
    [MaxLength(180)] public required string Title { get; set; }
    [MaxLength(1000)] public required string Message { get; set; }
    [MaxLength(30)] public string Type { get; set; } = "info";
    public bool IsRead { get; set; }
}
