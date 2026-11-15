# Ecommerce .NET API

ASP.NET Core backend for the Angular ecommerce project. It uses ASP.NET Core 10, Entity Framework Core, JWT authentication, and SQL Server; the Angular services are connected to these endpoints.

## Quick setup (Windows + LocalDB)

Requirements: .NET 10 SDK and SQL Server Express LocalDB.

From the `E-commerce` folder:

```powershell
.\api\setup-api.ps1
dotnet run --project .\api\Ecommerce.Api\Ecommerce.Api.csproj
```

The setup script starts LocalDB, restores/builds the API, creates `EcommerceLocal`, creates all tables, and inserts starter data. The API runs at `http://localhost:5080`. Verify it at `GET /api/health`.

The optional idempotent database bootstrap is at `database/00-create-database.sql`. Entity Framework creates the schema and seed data when the API first starts.

## Docker SQL Server alternative

```powershell
docker compose -f .\api\docker-compose.yml up -d
$env:ConnectionStrings__DefaultConnection='Server=localhost,1433;Database=EcommerceLocal;User Id=sa;Password=LocalDev!Passw0rd;TrustServerCertificate=True'
dotnet run --project .\api\Ecommerce.Api\Ecommerce.Api.csproj
```

Do not use the development password or JWT key in production. Supply both through secrets or environment variables.

## Seed accounts

| Role | Email | Password |
|---|---|---|
| Admin | `admin@dista.demo` | `AdminDemo123!` |
| Customer | `priya@example.com` | `Customer123!` |

Send the JWT returned by login as `Authorization: Bearer <token>`.

## API coverage

| Frontend area | Endpoints |
|---|---|
| Home | `GET /api/home` |
| Catalog / homeopathy | `GET /api/products`, `/api/categories`, `/api/brands` |
| Product detail | `GET /api/products/{idOrSlug}`, `POST /api/products/{id}/reviews` |
| Login / register | `/api/auth/register`, `/api/auth/login`, `/api/auth/me` |
| Cart | `GET /api/cart`, `PUT /api/cart/items`, delete item/cart |
| Wishlist | `GET/POST/DELETE /api/wishlist` |
| Checkout | address CRUD, coupon validation, `POST /api/checkout` |
| My orders | order list/detail/cancel under `/api/orders` |
| Blog | `GET /api/blog`, `GET /api/blog/{slug}` |
| Careers | job listing and application submission under `/api/careers` |
| Contact | `POST /api/contact` |
| Admin dashboard | `GET /api/admin/dashboard` |
| Admin products | CRUD under `/api/admin/products` |
| Admin orders | list/detail/status under `/api/admin/orders` |
| Admin customers | list/detail under `/api/admin/customers` |
| Admin categories/brands | CRUD under `/api/admin/categories` and `/api/admin/brands` |
| Admin inventory/procurement | `/api/admin/inventory`, `/api/admin/procurement` |
| Admin home manager | banner and home-section CRUD plus publish action |
| Admin blog/careers | CRUD plus application inbox |
| Admin reports/settings | `/api/admin/reports`, `/api/admin/settings` |
| Admin notifications/support | notifications, contact messages, job applications |

The About Us, Global Delivery, and Store Features pages are static presentation content, so no API was added for them. Payment processing is deliberately modeled as an order transaction only; a real payment provider can be connected in round two without storing card details.

## Useful examples

```powershell
$login = Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/auth/login -ContentType application/json -Body '{"email":"priya@example.com","password":"Customer123!"}'
$headers = @{ Authorization = "Bearer $($login.token)" }
Invoke-RestMethod -Uri http://localhost:5080/api/cart -Headers $headers
Invoke-RestMethod -Uri http://localhost:5080/api/orders -Headers $headers
```

## Frontend integration

The Angular API base URL is configured in `src/environments/environment.ts`. JWTs are attached by the HTTP interceptor, and the data-driven storefront and admin services now use these endpoints instead of DummyJSON and browser-only persistence.
