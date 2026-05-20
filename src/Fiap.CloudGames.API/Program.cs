using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Fiap.CloudGames.Api.Middlewares;
using Fiap.CloudGames.Application.Carts.Services;
using Fiap.CloudGames.Application.Games.Services;
using Fiap.CloudGames.Application.Games.Validators;
using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Consumers;
using Fiap.CloudGames.Application.Promotions.Services;
using Fiap.CloudGames.Application.UserGamesLibrary.Services;
using Fiap.CloudGames.Infrastructure;
using Fiap.CloudGames.Infrastructure.Auth;
using Fiap.CloudGames.Infrastructure.Games.Seeders;
using Fiap.CloudGames.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Serilog (Console + Loki)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Fiap.CloudGames.Catalog.Api")
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        uri: builder.Configuration["Loki:Url"] ?? "http://localhost:3100",
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "catalog-svc" },
            new LokiLabel { Key = "env", Value = builder.Environment.EnvironmentName.ToLower() }
        }
    )
    .CreateLogger();

// Substitui o logger padrão do .NET pelo Serilog
builder.Host.UseSerilog();

builder.Services.AddOptions<JwtOptions>()
	.Bind(builder.Configuration.GetSection("Jwt"))
	.Validate(o =>
	{
		o.Validate();
		return true;
	}, "JwtOptions validation")
	.ValidateOnStart();

builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();

builder.Services.AddInfrastructure(
	builder.Configuration,
	consumersAssembly: typeof(PaymentSucceededConsumer).Assembly,
	consumerEventTypes: [
		typeof(PaymentFailedConsumer),
		typeof(PaymentLinkGeneratedConsumer),
		typeof(PaymentRefundedConsumer),
		typeof(PaymentRefundFailedConsumer),
		typeof(PaymentSucceededConsumer),
	]);

builder.Services.AddControllers()
	.AddJsonOptions(o =>
	{
		o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
	});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateGameDtoValidator>();

builder.Services
	.AddAuthentication(o =>
	{
		o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
		o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
	})
	.AddJwtBearer(options =>
	{
		var secret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
		var key = Encoding.UTF8.GetBytes(secret);

		var validateIssuer = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]);
		var validateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]);

		options.RequireHttpsMetadata = false;
		options.SaveToken = true;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(key),
			ValidateIssuer = validateIssuer,
			ValidIssuer = builder.Configuration["Jwt:Issuer"],
			ValidateAudience = validateAudience,
			ValidAudience = builder.Configuration["Jwt:Audience"],
			ValidateLifetime = true
		};
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	var xmlFile = Path.ChangeExtension(Assembly.GetEntryAssembly()?.Location, ".xml");
	if (File.Exists(xmlFile)) c.IncludeXmlComments(xmlFile);

	c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fiap.CloudGames Catalog API", Version = "v1" });

	// JWT bearer auth in swagger
	c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Insira o token JWT no formato: Bearer {token}"
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

var app = builder.Build();

// Aviso se alguém rodar com secret de DEV fora de Development
if (!app.Environment.IsDevelopment())
{
	var cfg = app.Services.GetRequiredService<IConfiguration>();
	if (string.Equals(cfg["Jwt:Secret"], "dev-secret-ONLY-for-locals-please-change", StringComparison.Ordinal))
		app.Logger.LogWarning("Aplicação fora de Development usando secret de DEV. Verifique configuração de JWT.");
}

// ---- Migrate + Seed (dev) ----
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await db.Database.MigrateAsync();

	if (app.Environment.IsDevelopment())
	{
		var gameSeeder = scope.ServiceProvider.GetRequiredService<IGameSeeder>();
        using var ctsGameSeeder = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
        ctsGameSeeder.CancelAfter(TimeSpan.FromSeconds(30));
        await gameSeeder.SeedAsync(ctsGameSeeder.Token);
	}
}

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Liveness: Só diz que o processo está de pé
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: Diz se as dependências estão OK
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse 
});

app.MapControllers();

app.Run();
