using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ecommerce.Api.Contracts;
using Ecommerce.Api.Data;
using Ecommerce.Api.Domain;
using Ecommerce.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<TokenService>();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins("http://localhost:4200", "https://localhost:4200")
    .AllowAnyHeader().AllowAnyMethod()));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500 });
}));
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

if (args.Contains("--initialize-only", StringComparer.OrdinalIgnoreCase))
    return;

app.MapGet("/api/health", async (AppDbContext db) => Results.Ok(new
{
    status = await db.Database.CanConnectAsync() ? "healthy" : "unhealthy",
    database = db.Database.GetDbConnection().Database,
    utc = DateTime.UtcNow
}));

var auth = app.MapGroup("/api/auth").WithTags("Authentication");
auth.MapPost("/register", async (RegisterRequest request, AppDbContext db, TokenService tokens) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(email) || request.Password.Length < 8)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Name, a valid email, and an 8+ character password are required."] });
    if (await db.Users.AnyAsync(x => x.Email == email)) return Results.Conflict(new { message = "An account with this email already exists." });
    var user = new User { Name = request.Name.Trim(), Email = email, Phone = request.Phone, Role = "customer", PasswordHash = "" };
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", tokens.Create(user));
});
auth.MapPost("/login", async (LoginRequest request, AppDbContext db, TokenService tokens) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.IsActive);
    if (user is null || new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    return Results.Ok(tokens.Create(user));
});
auth.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
{
    var id = principal.UserId();
    var user = await db.Users.Where(x => x.Id == id).Select(x => new { x.Id, x.Name, x.Email, x.Phone, x.AvatarUrl, x.Role }).SingleOrDefaultAsync();
    return user is null ? Results.NotFound() : Results.Ok(user);
}).RequireAuthorization();

var storefront = app.MapGroup("/api").WithTags("Storefront");
storefront.MapGet("/home", async (AppDbContext db) => Results.Ok(new
{
    sections = await db.HomeSections.Where(x => x.IsEnabled && x.IsPublished).OrderBy(x => x.DisplayOrder).ToListAsync(),
    banners = await db.Banners.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ToListAsync(),
    categories = await db.Categories.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new { x.Id, x.Name, x.Slug, x.ImageUrl }).ToListAsync(),
    featuredProducts = await ProductQuery(db.Products.Where(x => x.IsActive && x.IsFeatured)).Take(12).ToListAsync()
}));
storefront.MapGet("/products", async (string? search, string? category, string? brand, decimal? minPrice, decimal? maxPrice, string? sort, int? page, int? pageSize, AppDbContext db) =>
{
    var currentPage = Math.Max(1, page ?? 1);
    var currentPageSize = Math.Clamp(pageSize ?? 24, 1, 100);
    var query = db.Products.AsNoTracking().Where(x => x.IsActive);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Sku.Contains(search) || x.Description.Contains(search));
    if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category!.Slug == category);
    if (!string.IsNullOrWhiteSpace(brand)) query = query.Where(x => x.Brand!.Slug == brand);
    if (minPrice.HasValue) query = query.Where(x => x.Price >= minPrice);
    if (maxPrice.HasValue) query = query.Where(x => x.Price <= maxPrice);
    query = sort switch { "price-asc" => query.OrderBy(x => x.Price), "price-desc" => query.OrderByDescending(x => x.Price), "name" => query.OrderBy(x => x.Name), _ => query.OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.CreatedAt) };
    var total = await query.CountAsync();
    var items = await ProductQuery(query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)).ToListAsync();
    return Results.Ok(new { items, page = currentPage, pageSize = currentPageSize, total, totalPages = (int)Math.Ceiling(total / (double)currentPageSize) });
});
storefront.MapGet("/products/{idOrSlug}", async (string idOrSlug, AppDbContext db) =>
{
    var numeric = int.TryParse(idOrSlug, out var id);
    var product = await db.Products.AsNoTracking().Where(x => x.IsActive && (numeric ? x.Id == id : x.Slug == idOrSlug))
        .Select(x => new
        {
            x.Id, x.Name, x.Slug, x.Sku, x.Description, x.ShortDescription, x.Price, x.OriginalPrice, x.StockQuantity,
            x.ImageUrl, x.GalleryJson, x.DetailsJson, category = new { x.CategoryId, x.Category!.Name, x.Category.Slug },
            brand = new { x.BrandId, x.Brand!.Name, x.Brand.Slug, x.Brand.LogoUrl },
            rating = x.Reviews.Where(r => r.IsApproved).Select(r => (double?)r.Rating).Average() ?? 0,
            reviews = x.Reviews.Where(r => r.IsApproved).OrderByDescending(r => r.CreatedAt).Select(r => new { r.Id, r.Rating, r.Title, r.Comment, r.CreatedAt, userName = r.User!.Name })
        }).SingleOrDefaultAsync();
    return product is null ? Results.NotFound() : Results.Ok(product);
});
storefront.MapPost("/products/{id:int}/reviews", async (int id, ReviewRequest request, ClaimsPrincipal principal, AppDbContext db) =>
{
    if (request.Rating is < 1 or > 5) return Results.BadRequest(new { message = "Rating must be between 1 and 5." });
    if (!await db.Products.AnyAsync(x => x.Id == id && x.IsActive)) return Results.NotFound();
    var userId = principal.UserId();
    var review = await db.Reviews.SingleOrDefaultAsync(x => x.ProductId == id && x.UserId == userId);
    if (review is null) { review = new Review { ProductId = id, UserId = userId, Rating = request.Rating, Title = request.Title, Comment = request.Comment }; db.Reviews.Add(review); }
    else { review.Rating = request.Rating; review.Title = request.Title; review.Comment = request.Comment; review.UpdatedAt = DateTime.UtcNow; }
    await db.SaveChangesAsync();
    return Results.Ok(review);
}).RequireAuthorization();
storefront.MapGet("/categories", async (AppDbContext db) => Results.Ok(await db.Categories.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ToListAsync()));
storefront.MapGet("/brands", async (AppDbContext db) => Results.Ok(await db.Brands.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()));

var cart = app.MapGroup("/api/cart").RequireAuthorization().WithTags("Cart");
cart.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) => Results.Ok(await CartQuery(db, p.UserId()).ToListAsync()));
cart.MapPut("/items", async (CartRequest request, ClaimsPrincipal p, AppDbContext db) =>
{
    if (request.Quantity < 1) return Results.BadRequest(new { message = "Quantity must be at least one." });
    var product = await db.Products.SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive);
    if (product is null) return Results.NotFound();
    if (request.Quantity > product.StockQuantity) return Results.Conflict(new { message = "Requested quantity is not available." });
    var userId = p.UserId();
    var item = await db.CartItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == request.ProductId && x.Variant == request.Variant);
    if (item is null) { item = new CartItem { UserId = userId, ProductId = request.ProductId, Quantity = request.Quantity, Variant = request.Variant }; db.CartItems.Add(item); }
    else { item.Quantity = request.Quantity; item.UpdatedAt = DateTime.UtcNow; }
    await db.SaveChangesAsync(); return Results.Ok(await CartQuery(db, userId).ToListAsync());
});
cart.MapDelete("/items/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) =>
{
    var item = await db.CartItems.SingleOrDefaultAsync(x => x.Id == id && x.UserId == p.UserId());
    if (item is null) return Results.NotFound(); db.CartItems.Remove(item); await db.SaveChangesAsync(); return Results.NoContent();
});
cart.MapDelete("/", async (ClaimsPrincipal p, AppDbContext db) => { await db.CartItems.Where(x => x.UserId == p.UserId()).ExecuteDeleteAsync(); return Results.NoContent(); });

var wishlist = app.MapGroup("/api/wishlist").RequireAuthorization().WithTags("Wishlist");
wishlist.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) => Results.Ok(await db.WishlistItems.Where(x => x.UserId == p.UserId()).Select(x => new { x.Id, x.ProductId, x.CreatedAt, product = new { x.Product!.Name, x.Product.Slug, x.Product.Price, x.Product.OriginalPrice, x.Product.ImageUrl, brand = x.Product.Brand!.Name, inStock = x.Product.StockQuantity > 0 } }).ToListAsync()));
wishlist.MapPost("/", async (WishlistRequest request, ClaimsPrincipal p, AppDbContext db) =>
{
    var userId = p.UserId(); if (!await db.Products.AnyAsync(x => x.Id == request.ProductId && x.IsActive)) return Results.NotFound();
    if (await db.WishlistItems.AnyAsync(x => x.UserId == userId && x.ProductId == request.ProductId)) return Results.Ok();
    var item = new WishlistItem { UserId = userId, ProductId = request.ProductId }; db.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/wishlist/{item.Id}", item);
});
wishlist.MapDelete("/{productId:int}", async (int productId, ClaimsPrincipal p, AppDbContext db) => { var count = await db.WishlistItems.Where(x => x.UserId == p.UserId() && x.ProductId == productId).ExecuteDeleteAsync(); return count == 0 ? Results.NotFound() : Results.NoContent(); });

var addresses = app.MapGroup("/api/addresses").RequireAuthorization().WithTags("Addresses");
addresses.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) => Results.Ok(await db.Addresses.Where(x => x.UserId == p.UserId()).OrderByDescending(x => x.IsDefault).ToListAsync()));
addresses.MapPost("/", async (AddressRequest r, ClaimsPrincipal p, AppDbContext db) => { var item = r.ToEntity(p.UserId()); if (r.IsDefault) await ClearDefaults(db, p.UserId()); db.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/addresses/{item.Id}", item); });
addresses.MapPut("/{id:int}", async (int id, AddressRequest r, ClaimsPrincipal p, AppDbContext db) =>
{
    var item = await db.Addresses.SingleOrDefaultAsync(x => x.Id == id && x.UserId == p.UserId()); if (item is null) return Results.NotFound();
    if (r.IsDefault) await ClearDefaults(db, p.UserId()); item.Apply(r); await db.SaveChangesAsync(); return Results.Ok(item);
});
addresses.MapDelete("/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) => { var item = await db.Addresses.SingleOrDefaultAsync(x => x.Id == id && x.UserId == p.UserId()); if (item is null) return Results.NotFound(); if (item.IsDefault) return Results.Conflict(new { message = "Choose another default address before deleting this one." }); db.Remove(item); await db.SaveChangesAsync(); return Results.NoContent(); });

storefront.MapPost("/coupons/validate", async (CouponRequest r, AppDbContext db) =>
{
    var code = r.Code.Trim().ToUpperInvariant(); var coupon = await db.Coupons.SingleOrDefaultAsync(x => x.Code == code && x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));
    if (coupon is null) return Results.NotFound(new { message = "Coupon is invalid or expired." });
    if (r.Subtotal < coupon.MinimumOrderValue) return Results.BadRequest(new { message = $"Minimum order value is {coupon.MinimumOrderValue:0.00}." });
    var discount = coupon.DiscountPercent.HasValue ? decimal.Round(r.Subtotal * coupon.DiscountPercent.Value / 100, 2) : Math.Min(r.Subtotal, coupon.DiscountAmount ?? 0);
    return Results.Ok(new { coupon.Code, coupon.Description, discount });
});
storefront.MapPost("/checkout", Checkout).RequireAuthorization();

var orders = app.MapGroup("/api/orders").RequireAuthorization().WithTags("Customer orders");
orders.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) => Results.Ok(await OrderQuery(db.Orders.Where(x => x.UserId == p.UserId()).OrderByDescending(x => x.CreatedAt)).ToListAsync()));
orders.MapGet("/{number}", async (string number, ClaimsPrincipal p, AppDbContext db) => { var order = await OrderQuery(db.Orders.Where(x => x.UserId == p.UserId() && x.OrderNumber == number)).SingleOrDefaultAsync(); return order is null ? Results.NotFound() : Results.Ok(order); });
orders.MapPost("/{number}/cancel", async (string number, ClaimsPrincipal p, AppDbContext db) => { var order = await db.Orders.SingleOrDefaultAsync(x => x.UserId == p.UserId() && x.OrderNumber == number); if (order is null) return Results.NotFound(); if (order.Status is "Shipped" or "Delivered" or "Cancelled") return Results.Conflict(new { message = "This order can no longer be cancelled." }); order.Status = "Cancelled"; order.Events.Add(new OrderEvent { Title = "Order cancelled", Actor = p.Identity?.Name ?? "Customer" }); await db.SaveChangesAsync(); return Results.Ok(order); });

storefront.MapGet("/blog", async (string? category, AppDbContext db) => { var q = db.BlogPosts.Where(x => x.IsPublished); if (!string.IsNullOrWhiteSpace(category)) q = q.Where(x => x.Category == category); return Results.Ok(await q.OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.PublishedAt).ToListAsync()); });
storefront.MapGet("/blog/{slug}", async (string slug, AppDbContext db) => { var item = await db.BlogPosts.SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished); return item is null ? Results.NotFound() : Results.Ok(item); });
storefront.MapGet("/careers", async (string? department, AppDbContext db) => { var q = db.JobOpenings.Where(x => x.IsActive); if (!string.IsNullOrWhiteSpace(department)) q = q.Where(x => x.Department == department); return Results.Ok(await q.OrderBy(x => x.Department).ThenBy(x => x.Title).ToListAsync()); });
storefront.MapPost("/careers/{id:int}/applications", async (int id, JobApplicationRequest r, AppDbContext db) => { if (!await db.JobOpenings.AnyAsync(x => x.Id == id && x.IsActive)) return Results.NotFound(); var item = new JobApplication { JobOpeningId = id, Name = r.Name.Trim(), Email = r.Email.Trim().ToLowerInvariant(), Phone = r.Phone, Note = r.Note, ResumeUrl = r.ResumeUrl }; db.Add(item); await db.SaveChangesAsync(); return Results.Accepted($"/api/careers/{id}/applications/{item.Id}", new { item.Id, item.Status }); });
storefront.MapPost("/contact", async (ContactRequest r, AppDbContext db) => { var item = new ContactMessage { Name = r.Name.Trim(), Email = r.Email.Trim().ToLowerInvariant(), Subject = r.Subject, OrderNumber = r.OrderNumber, Message = r.Message }; db.Add(item); await db.SaveChangesAsync(); return Results.Accepted($"/api/contact/{item.Id}", new { item.Id, item.Status }); });

var adminApi = app.MapGroup("/api/admin").RequireAuthorization(policy => policy.RequireRole("admin")).WithTags("Administration");
adminApi.MapGet("/dashboard", async (AppDbContext db) => Results.Ok(new
{
    counts = new { products = await db.Products.CountAsync(), customers = await db.Users.CountAsync(x => x.Role == "customer"), orders = await db.Orders.CountAsync(), lowStock = await db.Products.CountAsync(x => x.StockQuantity <= x.LowStockThreshold), unreadNotifications = await db.Notifications.CountAsync(x => !x.IsRead) },
    revenue = await db.Orders.Where(x => x.Status != "Cancelled").SumAsync(x => (decimal?)x.Total) ?? 0,
    recentOrders = await OrderQuery(db.Orders.OrderByDescending(x => x.CreatedAt).Take(8)).ToListAsync(),
    recentProducts = await ProductQuery(db.Products.OrderByDescending(x => x.CreatedAt).Take(8)).ToListAsync()
}));
adminApi.MapGet("/products", async (AppDbContext db) => Results.Ok(await ProductQuery(db.Products.OrderByDescending(x => x.CreatedAt)).ToListAsync()));
adminApi.MapPost("/products", async (ProductWrite r, AppDbContext db) => { var item = r.Create(); db.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/admin/products/{item.Id}", item); });
adminApi.MapPut("/products/{id:int}", async (int id, ProductWrite r, AppDbContext db) => { var item = await db.Products.FindAsync(id); if (item is null) return Results.NotFound(); r.Apply(item); await db.SaveChangesAsync(); return Results.Ok(item); });
adminApi.MapDelete("/products/{id:int}", async (int id, AppDbContext db) => { var item = await db.Products.FindAsync(id); if (item is null) return Results.NotFound(); item.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });
adminApi.MapGet("/orders", async (string? status, AppDbContext db) => { var q = db.Orders.AsQueryable(); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status); return Results.Ok(await OrderQuery(q.OrderByDescending(x => x.CreatedAt)).ToListAsync()); });
adminApi.MapGet("/orders/{number}", async (string number, AppDbContext db) => { var item = await OrderQuery(db.Orders.Where(x => x.OrderNumber == number)).SingleOrDefaultAsync(); return item is null ? Results.NotFound() : Results.Ok(item); });
adminApi.MapPut("/orders/{number}/status", async (string number, StatusRequest r, ClaimsPrincipal p, AppDbContext db) => { var item = await db.Orders.Include(x => x.Events).SingleOrDefaultAsync(x => x.OrderNumber == number); if (item is null) return Results.NotFound(); item.Status = r.Status; item.TrackingNumber = r.TrackingNumber ?? item.TrackingNumber; item.Courier = r.Courier ?? item.Courier; item.Events.Add(new OrderEvent { Title = $"Order {r.Status}", Actor = p.Identity?.Name ?? "Admin", Notes = r.Notes }); await db.SaveChangesAsync(); return Results.Ok(item); });
adminApi.MapGet("/customers", async (AppDbContext db) => Results.Ok(await db.Users.Where(x => x.Role == "customer").Select(x => new { x.Id, x.Name, x.Email, x.Phone, x.IsActive, x.CreatedAt, orderCount = db.Orders.Count(o => o.UserId == x.Id), lifetimeValue = db.Orders.Where(o => o.UserId == x.Id && o.Status != "Cancelled").Sum(o => (decimal?)o.Total) ?? 0 }).ToListAsync()));
adminApi.MapGet("/customers/{id:int}", async (int id, AppDbContext db) => { var item = await db.Users.Where(x => x.Id == id && x.Role == "customer").Select(x => new { x.Id, x.Name, x.Email, x.Phone, x.IsActive, addresses = x.Addresses, orders = db.Orders.Where(o => o.UserId == x.Id).Select(o => new { o.OrderNumber, o.Status, o.Total, o.CreatedAt }) }).SingleOrDefaultAsync(); return item is null ? Results.NotFound() : Results.Ok(item); });
adminApi.MapGet("/inventory", async (bool lowStockOnly, AppDbContext db) => { var q = db.Products.AsQueryable(); if (lowStockOnly) q = q.Where(x => x.StockQuantity <= x.LowStockThreshold); return Results.Ok(await q.Select(x => new { x.Id, x.Name, x.Sku, category = x.Category!.Name, brand = x.Brand!.Name, x.StockQuantity, x.LowStockThreshold, status = x.StockQuantity == 0 ? "Out of stock" : x.StockQuantity <= x.LowStockThreshold ? "Low stock" : "In stock" }).ToListAsync()); });
adminApi.MapPut("/inventory/{productId:int}", async (int productId, StockRequest r, AppDbContext db) => { var item = await db.Products.FindAsync(productId); if (item is null) return Results.NotFound(); item.StockQuantity = Math.Max(0, r.Quantity); item.LowStockThreshold = Math.Max(0, r.LowStockThreshold); await db.SaveChangesAsync(); return Results.Ok(item); });
adminApi.MapGet("/procurement", async (AppDbContext db) => Results.Ok(new { lowStockProducts = await db.Products.Where(x => x.IsActive && x.StockQuantity <= x.LowStockThreshold).Select(x => new { x.Id, x.Name, x.Sku, x.StockQuantity, x.LowStockThreshold }).ToListAsync(), orderItems = await db.OrderItems.Where(x => x.ProcurementStatus != "None").Select(x => new { x.Id, x.OrderId, x.ProductId, x.ProductName, x.Sku, x.Quantity, x.ProcurementStatus }).ToListAsync() }));
adminApi.MapPut("/procurement/items/{id:int}", async (int id, StatusRequest r, AppDbContext db) => { var item = await db.OrderItems.FindAsync(id); if (item is null) return Results.NotFound(); item.ProcurementStatus = r.Status; await db.SaveChangesAsync(); return Results.Ok(item); });

MapSimpleAdminCrud(adminApi, "categories", (AppDbContext db) => db.Categories, (Category x) => x.Id);
MapSimpleAdminCrud(adminApi, "brands", (AppDbContext db) => db.Brands, (Brand x) => x.Id);
MapSimpleAdminCrud(adminApi, "banners", (AppDbContext db) => db.Banners, (Banner x) => x.Id);
MapSimpleAdminCrud(adminApi, "home-sections", (AppDbContext db) => db.HomeSections, (HomeSection x) => x.Id);
MapSimpleAdminCrud(adminApi, "blog", (AppDbContext db) => db.BlogPosts, (BlogPost x) => x.Id);
MapSimpleAdminCrud(adminApi, "careers", (AppDbContext db) => db.JobOpenings, (JobOpening x) => x.Id);

adminApi.MapPost("/home-sections/publish", async (AppDbContext db) => { await db.HomeSections.ExecuteUpdateAsync(x => x.SetProperty(s => s.IsPublished, s => s.IsEnabled)); return Results.Ok(new { publishedAt = DateTime.UtcNow }); });
adminApi.MapGet("/reports", async (DateTime? from, DateTime? to, AppDbContext db) => { var start = from ?? DateTime.UtcNow.AddMonths(-6); var end = to ?? DateTime.UtcNow.AddDays(1); var q = db.Orders.Where(x => x.CreatedAt >= start && x.CreatedAt < end); return Results.Ok(new { from = start, to = end, grossSales = await q.Where(x => x.Status != "Cancelled").SumAsync(x => (decimal?)x.Total) ?? 0, ordersProcessed = await q.CountAsync(), averageOrderValue = await q.Where(x => x.Status != "Cancelled").AverageAsync(x => (decimal?)x.Total) ?? 0, cancelledOrders = await q.CountAsync(x => x.Status == "Cancelled"), salesByMonth = await q.Where(x => x.Status != "Cancelled").GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month }).Select(g => new { g.Key.Year, g.Key.Month, total = g.Sum(x => x.Total), orders = g.Count() }).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync() }); });
adminApi.MapGet("/settings", async (AppDbContext db) => Results.Ok(await db.StoreSettings.ToDictionaryAsync(x => x.Key, x => x.Value)));
adminApi.MapPut("/settings", async (Dictionary<string, string> settings, AppDbContext db) => { foreach (var pair in settings) { var item = await db.StoreSettings.SingleOrDefaultAsync(x => x.Key == pair.Key); if (item is null) db.Add(new StoreSetting { Key = pair.Key, Value = pair.Value }); else item.Value = pair.Value; } await db.SaveChangesAsync(); return Results.Ok(await db.StoreSettings.ToDictionaryAsync(x => x.Key, x => x.Value)); });
adminApi.MapGet("/notifications", async (AppDbContext db) => Results.Ok(await db.Notifications.OrderByDescending(x => x.CreatedAt).ToListAsync()));
adminApi.MapPut("/notifications/{id:int}/read", async (int id, AppDbContext db) => { var item = await db.Notifications.FindAsync(id); if (item is null) return Results.NotFound(); item.IsRead = true; await db.SaveChangesAsync(); return Results.NoContent(); });
adminApi.MapGet("/contact-messages", async (AppDbContext db) => Results.Ok(await db.ContactMessages.OrderByDescending(x => x.CreatedAt).ToListAsync()));
adminApi.MapGet("/job-applications", async (AppDbContext db) => Results.Ok(await db.JobApplications.Include(x => x.JobOpening).OrderByDescending(x => x.CreatedAt).ToListAsync()));

app.Run();

static IQueryable<object> ProductQuery(IQueryable<Product> query) => query.Select(x => new
{
    x.Id, x.Name, x.Slug, x.Sku, x.Price, x.OriginalPrice, x.StockQuantity, x.LowStockThreshold, x.ImageUrl, x.IsFeatured, x.IsActive,
    category = new { x.CategoryId, x.Category!.Name, x.Category.Slug }, brand = new { x.BrandId, x.Brand!.Name, x.Brand.Slug, x.Brand.LogoUrl },
    rating = x.Reviews.Where(r => r.IsApproved).Select(r => (double?)r.Rating).Average() ?? 0, reviewCount = x.Reviews.Count(r => r.IsApproved)
});

static IQueryable<object> CartQuery(AppDbContext db, int userId) => db.CartItems.Where(x => x.UserId == userId).Select(x => new
{
    x.Id, x.ProductId, x.Quantity, x.Variant, product = new { x.Product!.Name, x.Product.Slug, x.Product.Price, x.Product.OriginalPrice, x.Product.ImageUrl, x.Product.StockQuantity, brand = x.Product.Brand!.Name }, lineTotal = x.Product!.Price * x.Quantity
});

static IQueryable<object> OrderQuery(IQueryable<Order> query) => query.Select(x => new
{
    x.Id, x.OrderNumber, x.UserId, customer = new { x.User!.Name, x.User.Email, x.User.Phone }, x.Status, x.PaymentStatus, x.PaymentMethod, x.PaymentReference,
    x.Subtotal, x.Shipping, x.Discount, x.Tax, x.Total, x.TrackingNumber, x.Courier, x.Notes, x.CreatedAt,
    address = new { x.AddressId, x.Address!.Name, x.Address.Phone, x.Address.Pincode, x.Address.State, x.Address.City, x.Address.Locality, x.Address.HouseAddress, x.Address.Landmark, x.Address.Type },
    items = x.Items.Select(i => new { i.Id, i.ProductId, i.ProductName, i.Sku, i.ImageUrl, i.UnitPrice, i.Quantity, i.Variant, i.ProcurementStatus }),
    events = x.Events.OrderByDescending(e => e.CreatedAt).Select(e => new { e.Id, e.Title, e.Actor, e.Notes, e.CreatedAt })
});

static async Task<IResult> Checkout(CheckoutRequest request, ClaimsPrincipal principal, AppDbContext db)
{
    var userId = principal.UserId();
    var address = await db.Addresses.SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == userId);
    if (address is null) return Results.BadRequest(new { message = "Select a valid delivery address." });
    var cart = await db.CartItems.Include(x => x.Product).Where(x => x.UserId == userId).ToListAsync();
    if (cart.Count == 0) return Results.BadRequest(new { message = "Cart is empty." });
    var unavailable = cart.FirstOrDefault(x => !x.Product!.IsActive || x.Quantity > x.Product.StockQuantity);
    if (unavailable is not null) return Results.Conflict(new { message = $"{unavailable.Product!.Name} no longer has the requested stock." });
    var subtotal = cart.Sum(x => x.Product!.Price * x.Quantity); var shipping = subtotal >= 50 ? 0 : 4.99m; var discount = 0m;
    if (!string.IsNullOrWhiteSpace(request.CouponCode))
    {
        var code = request.CouponCode.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.SingleOrDefaultAsync(x => x.Code == code && x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));
        if (coupon is null || subtotal < coupon.MinimumOrderValue) return Results.BadRequest(new { message = "Coupon is invalid for this order." });
        if (code == "FREESHIP") shipping = 0; else discount = coupon.DiscountPercent.HasValue ? decimal.Round(subtotal * coupon.DiscountPercent.Value / 100, 2) : Math.Min(subtotal, coupon.DiscountAmount ?? 0);
    }
    var tax = decimal.Round(subtotal * .05m, 2); var total = subtotal - discount + shipping + tax;
    await using var transaction = await db.Database.BeginTransactionAsync();
    var order = new Order { OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", UserId = userId, AddressId = address.Id, Status = "Confirmed", PaymentMethod = request.PaymentMethod, PaymentStatus = request.PaymentMethod.Equals("cod", StringComparison.OrdinalIgnoreCase) ? "COD" : "Paid", PaymentReference = request.PaymentReference, Subtotal = subtotal, Shipping = shipping, Discount = discount, Tax = tax, Total = total, Notes = request.Notes };
    foreach (var line in cart) { line.Product!.StockQuantity -= line.Quantity; order.Items.Add(new OrderItem { ProductId = line.ProductId, ProductName = line.Product.Name, Sku = line.Product.Sku, ImageUrl = line.Product.ImageUrl, UnitPrice = line.Product.Price, Quantity = line.Quantity, Variant = line.Variant, ProcurementStatus = line.Product.StockQuantity <= line.Product.LowStockThreshold ? "Review" : "None" }); }
    order.Events.Add(new OrderEvent { Title = "Order confirmed", Actor = principal.Identity?.Name ?? "Customer", Notes = "Order placed from storefront checkout." });
    db.Orders.Add(order); db.CartItems.RemoveRange(cart); await db.SaveChangesAsync(); await transaction.CommitAsync();
    return Results.Created($"/api/orders/{order.OrderNumber}", new { order.OrderNumber, order.Status, order.PaymentStatus, order.Subtotal, order.Shipping, order.Discount, order.Tax, order.Total, estimatedDeliveryAt = DateTime.UtcNow.AddDays(5) });
}

static void MapSimpleAdminCrud<TEntity>(RouteGroupBuilder group, string route, Func<AppDbContext, DbSet<TEntity>> set, Func<TEntity, int> id) where TEntity : Entity
{
    group.MapGet($"/{route}", async (AppDbContext db) => Results.Ok(await set(db).AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
    group.MapPost($"/{route}", async (TEntity item, AppDbContext db) => { item.Id = 0; item.CreatedAt = item.UpdatedAt = DateTime.UtcNow; set(db).Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/admin/{route}/{id(item)}", item); });
    group.MapPut($"/{route}/{{entityId:int}}", async (int entityId, TEntity incoming, AppDbContext db) => { var item = await set(db).FindAsync(entityId); if (item is null) return Results.NotFound(); db.Entry(item).CurrentValues.SetValues(incoming); item.Id = entityId; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.Ok(item); });
    group.MapDelete($"/{route}/{{entityId:int}}", async (int entityId, AppDbContext db) => { var item = await set(db).FindAsync(entityId); if (item is null) return Results.NotFound(); set(db).Remove(item); await db.SaveChangesAsync(); return Results.NoContent(); });
}

static async Task ClearDefaults(AppDbContext db, int userId) => await db.Addresses.Where(x => x.UserId == userId && x.IsDefault).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsDefault, false));

public sealed record ProductWrite(string Name, string Slug, string Sku, string Description, string? ShortDescription, decimal Price, decimal? OriginalPrice, int StockQuantity, int LowStockThreshold, string? ImageUrl, string? GalleryJson, string? DetailsJson, bool IsFeatured, bool IsActive, int CategoryId, int BrandId)
{
    public Product Create() { var item = new Product { Name = Name, Slug = Slug, Sku = Sku, Description = Description, CategoryId = CategoryId, BrandId = BrandId }; Apply(item); return item; }
    public void Apply(Product x) { x.Name = Name; x.Slug = Slug; x.Sku = Sku; x.Description = Description; x.ShortDescription = ShortDescription; x.Price = Price; x.OriginalPrice = OriginalPrice; x.StockQuantity = Math.Max(0, StockQuantity); x.LowStockThreshold = Math.Max(0, LowStockThreshold); x.ImageUrl = ImageUrl; x.GalleryJson = GalleryJson; x.DetailsJson = DetailsJson; x.IsFeatured = IsFeatured; x.IsActive = IsActive; x.CategoryId = CategoryId; x.BrandId = BrandId; x.UpdatedAt = DateTime.UtcNow; }
}
public sealed record StockRequest(int Quantity, int LowStockThreshold);

public static class ApiExtensions
{
    public static int UserId(this ClaimsPrincipal principal) => int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();
    public static Address ToEntity(this AddressRequest r, int userId) => new() { UserId = userId, Name = r.Name, Phone = r.Phone, AlternatePhone = r.AlternatePhone, Pincode = r.Pincode, State = r.State, City = r.City, Locality = r.Locality, HouseAddress = r.HouseAddress, Landmark = r.Landmark, Type = r.Type, IsDefault = r.IsDefault };
    public static void Apply(this Address x, AddressRequest r) { x.Name = r.Name; x.Phone = r.Phone; x.AlternatePhone = r.AlternatePhone; x.Pincode = r.Pincode; x.State = r.State; x.City = r.City; x.Locality = r.Locality; x.HouseAddress = r.HouseAddress; x.Landmark = r.Landmark; x.Type = r.Type; x.IsDefault = r.IsDefault; x.UpdatedAt = DateTime.UtcNow; }
}

public partial class Program;
