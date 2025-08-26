using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrdersMini.Application.Validations;
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
builder.Services.AddAutoMapper(typeof(Program).Assembly); // pega Profiles do assembly Web e Application referenciado

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

// => auth
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

// => customers






















//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();


//var summaries = new[]
//{
//    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
//};

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

//app.Run();

//internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
//{
//    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
//}
