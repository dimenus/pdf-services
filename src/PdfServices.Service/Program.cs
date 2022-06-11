using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using PdfServices.Service.Utils;

namespace JsonMappingBuilder;

internal static class Program
{
    private const string AUTH0_DOMAIN_CONFIG = "Auth0:Domain";
    private const string AUTH0_AUDIENCE_CONFIG = "Auth0:Audience";

    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        
        // Add services to the container.
        var domain = $"https://{builder.Configuration["Auth0:Domain"]}/";
        if (!Uri.TryCreate(domain, UriKind.Absolute, out _))
            throw new Exception(
                $"required configuration item '{AUTH0_DOMAIN_CONFIG}' must point to a valid Auth0 tenant, eg 'foobar.us.auth0.com'");

        var audience = builder.Configuration[AUTH0_AUDIENCE_CONFIG] ??
                       throw new Exception($"required configuration item '{AUTH0_AUDIENCE_CONFIG} must be defined");
        builder.Services.AddAuthentication(opts => {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(opts => {
            opts.Authority = domain;
            opts.Audience = audience;
        });

        builder.Services.AddSingleton<SqliteDbContext>();
        builder.Services.AddControllers();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c => {
            // add JWT Authentication
            var security_scheme = new OpenApiSecurityScheme {
                Name = "JWT Authentication",
                Description = "Enter JWT Bearer token **_only_**",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer", // must be lower case
                BearerFormat = "JWT",
                Reference = new OpenApiReference {
                    Id = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };
            c.AddSecurityDefinition(security_scheme.Reference.Id, security_scheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement {
                {security_scheme, new string[] { }}
            });
            c.SwaggerDoc("v1", new OpenApiInfo {Title = "PdfServices", Version = "v1"});
        });

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}