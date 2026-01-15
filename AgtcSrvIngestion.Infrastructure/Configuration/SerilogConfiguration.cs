using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace AgtcSrvIngestion.Infrastructure.Configuration;

[ExcludeFromCodeCoverage]
public class SerilogConfiguration
{
    public static Serilog.Core.Logger ConfigureSerilog()
    {
        return new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] [CorrelationId: {CorrelationId}] {Message}{NewLine}{Exception}")
        .CreateLogger();
    }
}
