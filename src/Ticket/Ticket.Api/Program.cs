using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Persistence;
using Ticket.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
        return new BadRequestObjectResult(problem);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var envConnectionString = Environment.GetEnvironmentVariable("TICKET_DATABASE_URL");
var rawConnectionString = string.IsNullOrWhiteSpace(envConnectionString)
    ? builder.Configuration.GetConnectionString("Default")
    : envConnectionString;
if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is required. Set ConnectionStrings:Default or TICKET_DATABASE_URL.");
}

var connectionString = DbSeed.NormalizeNpgsqlConnectionString(rawConnectionString);
var databaseName = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database;
Console.WriteLine($"PostgreSQL database: {databaseName}");

builder.Services.AddDbContext<TicketDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TicketDbContext>());

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotFoundException).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(NotFoundException).Assembly);
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Ticket.Application.Behaviors.ValidationBehavior<,>));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
builder.Services.AddScoped<IAdminService, EfAdminService>();
builder.Services.Configure<AttachmentOptions>(builder.Configuration.GetSection(AttachmentOptions.SectionName));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ??
    [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:5173",
        "http://45.139.11.108",
        "http://45.139.11.108:80"
    ];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        // FE uses XMLHttpRequest/fetch with credentials mode "include" (withCredentials=true).
        // AllowCredentials requires explicit WithOrigins — never AllowAnyOrigin()/ "*".
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "userId"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

await DbSeed.EnsureSeededAsync(app.Services);

if (args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("DbSeed completed (--seed-only). Exiting.");
    return;
}

// CORS must run before ExceptionHandler so error/preflight responses also get Allow-Origin.
app.UseCors("Frontend");

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnauthorizedAppException => StatusCodes.Status401Unauthorized,
        ForbiddenAppException => StatusCodes.Status403Forbidden,
        BadRequestAppException => StatusCodes.Status400BadRequest,
        ValidationException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };

    if (exception is ValidationException ve)
    {
        var errors = ve.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g =>
                {
                    var name = g.Key;
                    var idx = name.LastIndexOf('.');
                    return idx >= 0 ? name[(idx + 1)..] : name;
                },
                g => g.Select(e => e.ErrorMessage).ToArray());
        await Results.ValidationProblem(errors).ExecuteAsync(context);
        return;
    }

    await Results.Problem(
        statusCode: status,
        title: status switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            _ => "An unexpected error occurred."
        },
        detail: exception?.Message).ExecuteAsync(context);
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Skip HTTPS redirect on IIS HTTP-only hosts — redirecting OPTIONS drops CORS headers.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
