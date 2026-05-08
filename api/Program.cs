using api.Configuration;
using api.Data;
using api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddOptions<OktaApiOptions>()
    .Bind(builder.Configuration.GetSection(OktaApiOptions.SectionName));

builder.Services
    .AddOptions<OktaManagementOptions>()
    .Bind(builder.Configuration.GetSection(OktaManagementOptions.SectionName));

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "app_data"));

builder.Services.AddDbContext<KycDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("KycDatabase")));

var oktaOptions = builder.Configuration
    .GetSection(OktaApiOptions.SectionName)
    .Get<OktaApiOptions>() ?? new OktaApiOptions();

var entraOptions = builder.Configuration
    .GetSection(EntraApiOptions.SectionName)
    .Get<EntraApiOptions>() ?? new EntraApiOptions();

builder.Services
    .AddAuthentication()
    .AddJwtBearer("Okta", options =>
    {
        options.Authority = oktaOptions.Issuer;
        options.Audience = oktaOptions.Audience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oktaOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = oktaOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "email"
        };
    })
    .AddJwtBearer("Entra", options =>
    {
        options.Authority = entraOptions.Authority;
        options.Audience = entraOptions.Audience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = entraOptions.Authority,
            ValidateAudience = true,
            ValidAudience = entraOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username"
        };
    });

builder.Services.AddControllers();
builder.Services.AddHttpClient<OktaManagementClient>();
builder.Services.AddScoped<DemoDataStore>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

await DatabaseSeeder.EnsureSeededAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
