using System.Security.Claims;
using System.Text;
using ApiRoleValidation.Data;
using ApiRoleValidation.Endpoints;
using ApiRoleValidation.Repositories;
using ApiRoleValidation.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ApiRoleValidation API",
        Version = "v1",
        Description = "Role validation and instant JWT revocation using HybridCache and SecurityStamp"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n" +
                      "Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\n" +
                      "Example: \"Bearer eyJhbGciOiJIUzI1NiIsIn...\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// EF Core InMemory Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("ApiRoleValidationDb"));

// HybridCache
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(60),
        LocalCacheExpiration = TimeSpan.FromMinutes(60)
    };
});
#pragma warning restore EXTEXP0018

// Application Services & Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserCacheService, UserCacheService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ApiRoleValidation";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ApiRoleValidationClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenSecurityStamp = context.Principal?.FindFirst("security_stamp")?.Value;

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(tokenSecurityStamp) || !Guid.TryParse(userIdStr, out var userId))
            {
                context.Fail("Token does not contain required user identity or security stamp claims.");
                return;
            }

            var cacheService = context.HttpContext.RequestServices.GetRequiredService<IUserCacheService>();
            var activeSecurityStamp = await cacheService.GetSecurityStampAsync(userId, context.HttpContext.RequestAborted);

            if (string.IsNullOrEmpty(activeSecurityStamp) || !string.Equals(activeSecurityStamp, tokenSecurityStamp, StringComparison.Ordinal))
            {
                context.Fail("User role or security stamp has changed. Token has been revoked.");
            }
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Seed initial database data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map route groups
app.MapAuthEndpoints();
app.MapOrderEndpoints();
app.MapReportEndpoints();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }

