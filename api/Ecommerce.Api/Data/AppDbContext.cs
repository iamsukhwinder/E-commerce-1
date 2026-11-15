using Ecommerce.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<HomeSection> HomeSections => Set<HomeSection>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Brand>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        modelBuilder.Entity<BlogPost>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<HomeSection>().HasIndex(x => x.SectionKey).IsUnique();
        modelBuilder.Entity<StoreSetting>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<CartItem>().HasIndex(x => new { x.UserId, x.ProductId, x.Variant }).IsUnique();
        modelBuilder.Entity<WishlistItem>().HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        modelBuilder.Entity<Review>().HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        modelBuilder.Entity<Order>().HasOne(x => x.Address).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderItem>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
