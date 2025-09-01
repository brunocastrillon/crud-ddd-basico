using AutoMapper;
using FluentValidation;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using OrdersMini.Application.DTOs;
using OrdersMini.Application.Mappings;
using OrdersMini.Application.Validations;
using OrdersMini.Domain.Entities;
using OrdersMini.Infrastructure;
using OrdersMini.Infrastructure.Seed;
using OrdersMini.Web;

using Serilog;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// === Serilog ===
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// === DbContext ===
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// === AutoMapper ===
builder.Services.AddAutoMapper(typeof(Profiles).Assembly,         // carrega os Profiles do Application
                               typeof(Program).Assembly           // (opcional) também do Web, se tiver Profiles lá
);

// === Validators ===
builder.Services.AddValidatorsFromAssemblyContaining<CustomerValidation>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductValidation>();
builder.Services.AddValidatorsFromAssemblyContaining<OrderValidation>();

// === ProblemDetails + Exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// === Auth (JWT) ===
IConfigurationSection jwtSection = builder.Configuration.GetSection("JWT");
SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
                                                                            {
                                                                                o.TokenValidationParameters = new TokenValidationParameters
                                                                                {
                                                                                    ValidateIssuer = true,
                                                                                    ValidateAudience = true,
                                                                                    ValidateLifetime = true,
                                                                                    ValidateIssuerSigningKey = true,
                                                                                    ValidIssuer = jwtSection["Issuer"],
                                                                                    ValidAudience = jwtSection["Audience"],
                                                                                    IssuerSigningKey = key
                                                                                };
                                                                            });

builder.Services.AddAuthorizationBuilder().AddPolicy("WriteAccess", p => p.RequireAuthenticatedUser())
                                          .AddPolicy("CanDelete", p => p.RequireRole("Admin"));

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "", Version = "" });

    OpenApiSecurityScheme scheme = new()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe: Bearer {token}"
    };

    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new() { [scheme] = new List<string>() });
});

// === Init ===
WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

#if DEBUG
app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();
#endif

// === Seed ===
using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await AppDbSeeder.SeedAsync(db);
}

// === Login Fake ===
var users = new[]
{
    new { UsernName = "admin", Password = "123", Role = "Admin" },
    new { UsernName = "user", Password = "123", Role = "User" }
};

// === Endpoints ===

#region Auth
app.MapPost("/api/auth/login", (string username, string password) =>
{
    var user = users.SingleOrDefault(x => x.UsernName == username && x.Password == password);
    if (user is null) return Results.Unauthorized();

    Claim[] claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.UsernName),
        new Claim(JwtRegisteredClaimNames.Sub, user.UsernName),
        new Claim(JwtRegisteredClaimNames.Sub, user.UsernName)
};

    SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    JwtSecurityToken token = new JwtSecurityToken(issuer: jwtSection["Issuer"],
                                                  audience: jwtSection["Audience"],
                                                  claims: claims,
                                                  expires: DateTime.UtcNow.AddHours(8),
                                                  signingCredentials: credentials);

    return Results.Ok(new
    {
        access_token = new JwtSecurityTokenHandler().WriteToken(token)
    });
});
#endregion

#region Customers
app.MapPost("/api/customers", async (CustomerRequest dto, IValidator<CustomerRequest> validator, AppDbContext db, IMapper map) =>
{
    FluentValidation.Results.ValidationResult validate = await validator.ValidateAsync(dto);
    if (!validate.IsValid) return Results.ValidationProblem(validate.ToDictionary());

    if (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == dto.Email))
        return Results.Problem(title: "E-mail já cadatrado", statusCode: 409);

    Customer customer = new(dto.Name, dto.Email);

    db.Customers.Add(customer);
    await db.SaveChangesAsync();

    return Results.Created($"/api/customers/{customer.Id}", map.Map<CustomerResponse>(customer));
});//.RequireAuthorization("WriteAccess");

app.MapGet("/api/customers", async (string? search, int page, int pageSize, IValidator<CustomerRequest> validator, AppDbContext db, IMapper map) =>
{
    //page = 1;
    //pageSize = 10;

    IQueryable<Customer> query = db.Customers.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(search))
    {
        string s = search.ToLower();
        query = query.Where(c => EF.Functions.Like(c.Name.ToLower(), $"%{s}%") ||
                                 EF.Functions.Like(c.Email.ToLower(), $"%{s}%"));
    }

    int total = await query.CountAsync();

    // - projeção direta no bd visando perfomance
    List<CustomerResponse> pageData = await query.OrderByDescending(c => c.Id)
                                                 .Skip((page - 1) * pageSize)
                                                 .Take(pageSize)
                                                 .Select(c => new CustomerResponse(c.Id, c.Name, c.Email, c.CreatedAt))
                                                 .ToListAsync();
    
    // - consulta para ser utilizada em com o automapper
    //List<Customer> data = await query.OrderByDescending(c => c.Id)
    //                                 .Skip((page - 1) * pageSize)
    //                                 .Take(pageSize)
    //                                 .ToListAsync();

    return Results.Ok(new
    {
        total,
        page,
        pageSize,
        items = pageData // - resultado usando automapper ==> data.Select(c => map.Map<CustomerResponse>(c))
    });
});

app.MapGet("/api/customers/{id:int}", async (int id, AppDbContext db, IMapper map) => await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(map.Map<CustomerResponse>(c)) : Results.NotFound());

app.MapPut("/api/customers/{id:int}", async (int id, CustomerRequest dto, IValidator<CustomerRequest> validator, AppDbContext db) =>
{
    FluentValidation.Results.ValidationResult validate = await validator.ValidateAsync(dto);
    if (!validate.IsValid) return Results.ValidationProblem(validate.ToDictionary());

    Customer? customer = await db.Customers.FindAsync(id);
    if (customer is null) return Results.NotFound();

    // - muta a ENTIDADE rastreada
    customer.Update(dto.Name, dto.Email);

    await db.SaveChangesAsync();

    return Results.NoContent();
});//.RequireAuthorization("WriteAccess");

app.MapDelete("/api/customers/{id:int}", async (int id, AppDbContext db) =>
{
    var customer = await db.Customers.FindAsync(id);

    if (customer is null) return Results.NotFound();

    customer.SetDelete();

    await db.SaveChangesAsync();

    return Results.NoContent();
});//.RequireAuthorization("CanDelete");
#endregion

#region Products
app.MapPost("/api/products", async (ProductRequest dto, IValidator<ProductRequest> v, AppDbContext db, IMapper map) =>
{
    FluentValidation.Results.ValidationResult val = await v.ValidateAsync(dto);
    if (!val.IsValid) return Results.ValidationProblem(val.ToDictionary());

    if (await db.Products.IgnoreQueryFilters().AnyAsync(c => c.Description == dto.Description))
        return Results.Problem(title: "Produto já cadatrado", statusCode: 409);

    Product product = new(dto.Description, dto.Price);

    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", map.Map<ProductResponse>(product));
});//.RequireAuthorization("WriteAccess");

app.MapGet("/api/products", async (decimal? minPrice, decimal? maxPrice, string? search, string? sort, int page, int pageSize, IValidator<CustomerRequest> validator, AppDbContext db, IMapper map) =>
{
    //page = 1;
    //pageSize = 10;

    IQueryable<Product> query = db.Products.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.ToLower();
        query = query.Where(p => EF.Functions.Like(p.Description.ToLower(), $"%{s}%"));
    }

    if (minPrice is not null) query = query.Where(p => p.Price >= minPrice);
    if (maxPrice is not null) query = query.Where(p => p.Price <= maxPrice);

    int total = await query.CountAsync();

    query = sort?.ToLower() switch
    {
        "price_asc" => query.OrderBy(p => p.Price),
        "price_desc" => query.OrderByDescending(p => p.Price),
        "desc_asc" => query.OrderBy(p => p.Description),
        "desc_desc" => query.OrderByDescending(p => p.Description),
        _ => query.OrderBy(p => p.Id)
    };

    //- projeção direta no bd visando perfomance
    List<ProductResponse> pageData = await query.Skip((page - 1) * pageSize)
                                                .Take(pageSize)
                                                .Select(p => new ProductResponse(p.Id, p.Description, p.Price, p.CreatedAt))
                                                .ToListAsync();

    // - consulta para ser utilizada em com o automapper
    //List<Product> pageData = await query.OrderByDescending(p => p.Id)
    //                                    .Skip((page - 1) * pageSize)
    //                                    .Take(pageSize)
    //                                    .ToListAsync();
    //var data = await query.ToListAsync();
    //return Results.Ok(data.Select(map.Map<ProductResponse>));

    return Results.Ok(new
    {
        total,
        page,
        pageSize,
        items = pageData // - resultado usando automapper ==> data.Select(p => map.Map<ProductResponse>(p))
    });
});

app.MapGet("/api/products/{id:int}", async (int id, AppDbContext db, IMapper map) => await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(map.Map<ProductResponse>(p)) : Results.NotFound());

app.MapPut("/api/products/{id:int}", async (int id, ProductRequest dto, IValidator<ProductRequest> v, AppDbContext db) =>
{
    FluentValidation.Results.ValidationResult val = await v.ValidateAsync(dto);
    if (!val.IsValid) return Results.ValidationProblem(val.ToDictionary());

    Product? product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    product.Update(dto.Description, dto.Price);

    await db.SaveChangesAsync();

    return Results.NoContent();
});//.RequireAuthorization("WriteAccess");

app.MapDelete("/api/products/{id:int}", async (int id, AppDbContext db) =>
{
    Product? product = await db.Products.FindAsync(id);

    if (product is null) return Results.NotFound();

    product.SetDelete();

    await db.SaveChangesAsync();

    return Results.NoContent();
});//.RequireAuthorization("CanDelete");
#endregion

#region Orders

#endregion







app.Run();














//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();

//app.MapGet("/weatherforecast", () =>
//{
//    var forecast = Enumerable.Range(1, 5).Select(index =>
//        new WeatherForecast
//        (
//            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//            Random.Shared.Next(-20, 55),
//            summaries[Random.Shared.Next(summaries.Length)]
//        ))
//        .ToArray();
//    return forecast;
//})
//.WithName("GetWeatherForecast");