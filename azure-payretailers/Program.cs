using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "APP SERVICE NET")) // Equivalente a OTEL_RESOURCE_ATTRIBUTES=service.name=OPENTEL
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

var app = builder.Build();

// Hello World endpoint
app.MapGet("/", () => Results.Ok("La aplicación está funcionando correctamente"));

// Run the app
app.Run();
