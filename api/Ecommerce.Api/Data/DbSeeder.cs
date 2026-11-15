using System.Text.Json;
using Ecommerce.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var hasher = new PasswordHasher<User>();
        var admin = new User { Name = "Store Admin", Email = "admin@dista.demo", PasswordHash = "", Role = "admin", Phone = "+91 90000 00000" };
        admin.PasswordHash = hasher.HashPassword(admin, "AdminDemo123!");
        var customer = new User { Name = "Priya Sharma", Email = "priya@example.com", PasswordHash = "", Role = "customer", Phone = "+91 98765 43210" };
        customer.PasswordHash = hasher.HashPassword(customer, "Customer123!");
        db.Users.AddRange(admin, customer);

        var categories = new[]
        {
            new Category { Name = "Ayurveda & Wellness", Slug = "wellness", ImageUrl = "/images/explore-category/image 38.png", DisplayOrder = 1 },
            new Category { Name = "Homeopathy", Slug = "homeopathy", ImageUrl = "/images/explore-category/image 39.png", DisplayOrder = 2 },
            new Category { Name = "Beauty & Personal Care", Slug = "beauty", ImageUrl = "/images/explore-category/image 40.png", DisplayOrder = 3 },
            new Category { Name = "Indian Pantry", Slug = "pantry", ImageUrl = "/images/explore-category/image 41.png", DisplayOrder = 4 },
            new Category { Name = "Ethnic Wear", Slug = "ethnic", ImageUrl = "/images/explore-category/image 42.png", DisplayOrder = 5 },
            new Category { Name = "Handicrafts", Slug = "handicrafts", ImageUrl = "/images/explore-category/image 43.png", DisplayOrder = 6 }
        };
        db.Categories.AddRange(categories);

        var brands = new[]
        {
            new Brand { Name = "Himalaya", Slug = "himalaya", LogoUrl = "/images/brands/himalaya.svg" },
            new Brand { Name = "Dabur", Slug = "dabur", LogoUrl = "/images/brands/dabur.svg" },
            new Brand { Name = "Patanjali", Slug = "patanjali", LogoUrl = "/images/brands/patanjali.svg" },
            new Brand { Name = "SBL", Slug = "sbl", LogoUrl = "/images/brands/sbl.svg" },
            new Brand { Name = "Dr. Reckeweg", Slug = "reckeweg", LogoUrl = "/images/brands/reckeweg.svg" },
            new Brand { Name = "Organic India", Slug = "organic-india", LogoUrl = "/images/brands/organic-india.svg" }
        };
        db.Brands.AddRange(brands);
        await db.SaveChangesAsync();

        var productSeed = new[]
        {
            ("Himalaya Liv.52 Tablets", "himalaya-liv52", "HIM-LIV52-100", 14.99m, 18.99m, 40, 0, 0, "/images/wellness/himalaya-liv52.jpg"),
            ("Dabur Chyawanprash", "dabur-chyawanprash", "DAB-CHY-500", 11.49m, 14.99m, 32, 0, 1, "/images/wellness/dabur-chyawanprash.jpg"),
            ("Patanjali Giloy Ghanvati", "patanjali-giloy", "PAT-GIL-60", 8.99m, 10.99m, 18, 0, 2, "/images/wellness/patanjali-giloy.jpg"),
            ("Organic India Ashwagandha", "organic-ashwagandha", "ORG-ASH-60", 16.99m, 21.99m, 24, 0, 5, "/images/wellness/ashwagandha.jpg"),
            ("SBL Arnica Montana 30C", "sbl-arnica-30c", "SBL-ARN-30", 7.49m, 8.99m, 50, 1, 3, "/images/homeopathy/sbl-arnica.jpg"),
            ("SBL Scalptone Tablets", "sbl-scalptone", "SBL-SCA-25", 9.99m, 12.49m, 11, 1, 3, "/images/homeopathy/sbl-scalptone.jpg"),
            ("Dr. Reckeweg R1 Inflammation Drops", "reckeweg-r1", "REC-R1-22", 18.50m, 22.00m, 8, 1, 4, "/images/homeopathy/reckeweg-r1.jpg"),
            ("Neem & Turmeric Capsules", "neem-turmeric", "ORG-NEE-60", 13.25m, 16.50m, 4, 0, 5, "/images/wellness/neem-turmeric.jpg")
        };
        foreach (var p in productSeed)
        {
            db.Products.Add(new Product
            {
                Name = p.Item1, Slug = p.Item2, Sku = p.Item3, Price = p.Item4, OriginalPrice = p.Item5,
                StockQuantity = p.Item6, CategoryId = categories[p.Item7].Id, BrandId = brands[p.Item8].Id,
                ImageUrl = p.Item9, IsFeatured = true, ShortDescription = "Authentic product sourced from trusted Indian brands.",
                Description = $"{p.Item1} is carefully sourced and quality checked for global delivery.",
                GalleryJson = JsonSerializer.Serialize(new[] { p.Item9 }),
                DetailsJson = JsonSerializer.Serialize(new { benefits = new[] { "Authenticity verified", "Quality checked" }, usage = "Use as directed on the product label." })
            });
        }

        db.Addresses.Add(new Address
        {
            UserId = customer.Id, Name = customer.Name, Phone = customer.Phone!, Pincode = "110001", State = "Delhi",
            City = "New Delhi", Locality = "Connaught Place", HouseAddress = "Flat 402, Royal Residency, Block C",
            Landmark = "Near Statesman House", Type = "Home", IsDefault = true
        });
        db.Coupons.AddRange(
            new Coupon { Code = "SAVE10", DiscountPercent = 10, MinimumOrderValue = 20, Description = "10% off orders above $20" },
            new Coupon { Code = "WELCOME50", DiscountAmount = 5, MinimumOrderValue = 30, Description = "$5 off orders above $30" },
            new Coupon { Code = "FREESHIP", DiscountAmount = 4.99m, MinimumOrderValue = 0, Description = "Free express delivery" });
        db.Banners.AddRange(
            new Banner { Section = "hero", Title = "Authentic Ayurveda & Wellness", Subtitle = "Delivered worldwide", ImageUrl = "/images/hero-carousel/Ayurvedic.png", LinkUrl = "/products?category=wellness", DisplayOrder = 1 },
            new Banner { Section = "hero", Title = "Trusted Homeopathy", Subtitle = "Leading brands from India and Germany", ImageUrl = "/images/hero-carousel/Homeopathy.png", LinkUrl = "/homeopathy", DisplayOrder = 2 });
        db.HomeSections.AddRange(
            new HomeSection { SectionKey = "hero", Type = "carousel", Title = "Hero carousel", DisplayOrder = 1 },
            new HomeSection { SectionKey = "categories", Type = "category-grid", Title = "Explore categories", DisplayOrder = 2 },
            new HomeSection { SectionKey = "featured", Type = "product-grid", Title = "Trending products", ConfigurationJson = "{\"featuredOnly\":true,\"limit\":8}", DisplayOrder = 3 });
        db.BlogPosts.AddRange(
            new BlogPost { Slug = "homeopathy-potencies-guide", Title = "The Beginner's Guide to Homeopathic Dilutions", Category = "Homeopathy & Remedies", Excerpt = "Understanding centesimal, decimal and Q potencies.", Content = "A practical introduction to common homeopathic dilution systems.", ImageUrl = "/images/homeopathy/homeopathy-banner.jpg", AuthorName = "Dr. Meera Swaminathan", AuthorRole = "Consultant Homeopath", ReadTimeMinutes = 6, IsFeatured = true },
            new BlogPost { Slug = "ashwagandha-sleep-science", Title = "Ashwagandha for Stress & Restful Sleep", Category = "Ayurveda & Wellness", Excerpt = "What modern clinical research says.", Content = "An overview of ashwagandha, withanolides and responsible use.", ImageUrl = "/images/wellness/ashwagandha.jpg", AuthorName = "Vaidya Anirudh Joshi", AuthorRole = "Ayurvedic Physician", ReadTimeMinutes = 5 });
        db.JobOpenings.AddRange(
            new JobOpening { Title = "Senior Frontend Engineer (Angular)", Department = "Engineering", Location = "Remote (USA / India)", EmploymentType = "Full-time", Experience = "4+ Years", Description = "Build high-performance storefront experiences." },
            new JobOpening { Title = "Global Customer Experience Specialist", Department = "Customer Experience", Location = "Remote", EmploymentType = "Full-time", Experience = "2+ Years", Description = "Support global customers with orders and product queries." });
        db.StoreSettings.AddRange(
            new StoreSetting { Key = "storeName", Value = "Dista Marketplace" },
            new StoreSetting { Key = "supportEmail", Value = "support@example.com" },
            new StoreSetting { Key = "currency", Value = "USD" },
            new StoreSetting { Key = "timezone", Value = "Asia/Kolkata" });
        db.Notifications.Add(new Notification { UserId = admin.Id, Title = "API initialized", Message = "The local ecommerce database is ready.", Type = "success" });
        await db.SaveChangesAsync();
    }
}
