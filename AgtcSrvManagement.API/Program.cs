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

string jwtSigningKey;

if (!builder.Environment.IsDevelopment())
{
    Log.Information("Ambiente de Produ��o. Buscando segredos do AWS Parameter Store.");
    var ssmClient = new AmazonSimpleSystemsManagementClient();

    var jwtParameterName = builder.Configuration["ParameterStore:JwtSigningKey"];
    var jwtResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
    {
        Name = jwtParameterName,
        WithDecryption = true
    });
    jwtSigningKey = jwtResponse.Parameter.Value;

    var s3Bucket = builder.Configuration["DataProtection:S3BucketName"];
    var s3KeyPrefix = builder.Configuration["DataProtection:S3KeyPrefix"];
    var s3DataProtectionConfig = new S3XmlRepositoryConfig(s3Bucket)
    {
        KeyPrefix = s3KeyPrefix
    };

    builder.Services.AddDataProtection()
        .SetApplicationName("FiapSrvPayment")
        .PersistKeysToAwsS3(s3DataProtectionConfig);
}
else
{
    Log.Information("Ambiente de Desenvolvimento. Usando appsettings.json.");
    jwtSigningKey = builder.Configuration["Jwt:DevKey"]!;
}

builder.Services.ConfigureJwtBearer(builder.Configuration, jwtSigningKey);
builder.Services.AddAuthorization();


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "FIAP Cloud Games - Payment API", Version = "v1" });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT no formato: Bearer {seu token}"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandler>();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.UseHttpMetrics();

app.MapMetrics();
app.MapControllers();

app.Run();
