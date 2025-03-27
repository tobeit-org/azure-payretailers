using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;  // Para ILogger

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "APP SERVICE NET"))
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://payretailers-otel-collector.interno.tobeit.net/v1/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metricsBuilder => metricsBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://payretailers-otel-collector.interno.tobeit.net/v1/metrics");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

// Configure logging (optional)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole(); // Esto agregará la salida de logs a la consola.
});

var app = builder.Build();

// Configure the error handling endpoint
app.MapGet("/error", () =>
{
    throw new Exception("This is a forced error!");
});

// Hello World endpoint
app.MapGet("/", () => Results.Ok("La aplicación está funcionando correctamente"));

// Error handling middleware
app.UseExceptionHandler("/error"); // Redirigir a /error en caso de excepción

// Run the app
app.Run();
