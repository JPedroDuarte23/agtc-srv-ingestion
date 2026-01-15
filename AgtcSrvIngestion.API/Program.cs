using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Prometheus;
using AgtcSrvIngestion.Infrastructure.Configuration;
using AgtcSrvIngestion.Infrastructure.Middleware;
using AgtcSrvIngestion.Application.Interfaces;
using AgtcSrvIngestion.Application.Services;

[assembly: ExcludeFromCodeCoverage]

var builder = WebApplication.CreateBuilder(args);

Log.Logger = SerilogConfiguration.ConfigureSerilog();
builder.Host.UseSerilog();

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSimpleSystemsManagement>();
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
builder.Services.AddAWSService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>();

builder.Services.AddScoped<ITelemetryService, TelemetryService>();

string jwtSigningKey;

if (!builder.Environment.IsDevelopment())
{
    Log.Information("Ambiente de Produção detectado. Buscando segredos no AWS Parameter Store e S3.");

    var ssmClient = new AmazonSimpleSystemsManagementClient(); 
    var jwtParameterName = builder.Configuration["ParameterStore:JwtSigningKey"];
    try
    {
        var jwtResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = jwtParameterName,
            WithDecryption = true
        });
        jwtSigningKey = jwtResponse.Parameter.Value;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Falha crítica ao buscar segredo no Parameter Store. Verifique as permissões da Role ou o nome do parâmetro.");
        throw;
    }

    // B. Configura Data Protection no S3 (Persistência de chaves de criptografia)
    var s3Bucket = builder.Configuration["DataProtection:S3BucketName"];
    var s3KeyPrefix = builder.Configuration["DataProtection:S3KeyPrefix"] ?? "DataProtection-Keys";

    var s3DataProtectionConfig = new S3XmlRepositoryConfig(s3Bucket)
    {
        KeyPrefix = s3KeyPrefix
    };

    builder.Services.AddDataProtection()
        .SetApplicationName("AgroIngestionAPI")
        .PersistKeysToAwsS3(s3DataProtectionConfig);
}
else
{
    Log.Information("Ambiente de Desenvolvimento. Usando chave estática do appsettings.");
    jwtSigningKey = builder.Configuration["Jwt:Secret"] ?? builder.Configuration["Jwt:DevKey"]!;

    if (string.IsNullOrEmpty(jwtSigningKey))
    {
        throw new ArgumentNullException("Jwt:Secret", "A chave JWT não foi encontrada no appsettings.Development.json");
    }
}

// 5. Configura o JWT com a chave recuperada acima
builder.Services.ConfigureJwtBearer(builder.Configuration, jwtSigningKey);
builder.Services.AddAuthorization();

// 6. Configuração de API (Controllers, Swagger, JSON)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "Agro Ingestion API", Version = "v1" });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT."
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
       {
           new OpenApiSecurityScheme
           {
               Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
           },
           Array.Empty<string>()
       }
    });
});

var app = builder.Build();

// --- Pipeline ---

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandler>();

app.UseHttpsRedirection();

app.UseHttpMetrics();
app.MapMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

try
{
    Log.Information("Iniciando Agro.Ingestion.API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}