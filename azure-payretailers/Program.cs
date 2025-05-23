using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Serilog;
using Serilog.Sinks.Network;
using Serilog.Formatting.Json;
using OpenTelemetry.Logs;
using System;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Initialize Serilog first and configure logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()  // Optional: Write logs to the console
    .WriteTo.TCPSink("tcp://localhost:8888", new JsonFormatter())  // Logstash TCP input on port 8888 with JSON format
    .CreateLogger();

// Add Serilog to the logging pipeline
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();  // Optional: Clear default logging providers
    logging.AddSerilog();      // Add Serilog as the logging provider
    logging.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage=true;
        options.IncludeScopes = true;
        options.AddOtlpExporter(options =>
        {
        options.Endpoint = new Uri("https://payretailers-otel-collector.interno.tobeit.net/v1/logs");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
    });
});


//custom Tracing Spans
const string CustomTelemetrySource = "CustomTelemetrySource";
ActivitySource CustomTracer = new ActivitySource(CustomTelemetrySource);

// OpenTelemetry configuration (tracing, metrics, and logs)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "APP SERVICE NET",
        serviceNamespace: "dev",
        serviceVersion: "1.0.0",
        serviceInstanceId: "local-TOBEIT-LAPTOP")
        .AddAttributes(new Dictionary<string, object>
        {
            { "deployment.environment", "stg" } // Usar deployment.environment como en la documentación de Elastic
        }))
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .SetSampler(new AlwaysOnSampler())
        .AddSource(CustomTelemetrySource) //add custom Traces as a source
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
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://payretailers-otel-collector.interno.tobeit.net/v1/metrics");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithLogging(logsBuilder => logsBuilder
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://payretailers-otel-collector.interno.tobeit.net/v1/logs");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));;

var app = builder.Build();

// Enable request logging middleware to capture HTTP requests and responses
// app.UseSerilogRequestLogging();  // Uncomment to log HTTP requests
app.MapPrometheusScrapingEndpoint("/metrics");
// Configure the error handling endpoint
app.MapGet("/error", () =>
{   
    throw new Exception("This is a forced error!");
});

// Hello World endpoint
app.MapGet("/", () => Results.Ok("La aplicación está funcionando correctamente"));

// Define a new endpoint "/transaction_data" to generate and log payment data
app.MapGet("/transaction_data", () =>
{
    var activity = Activity.Current;
    activity?.AddEvent(new("Starting Main Span"));
    // var tracer = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
    //     .Build()
    //     .GetTracer("MyAppTracer");
    using var activity2 = CustomTracer.StartActivity("GeneratePaymentLog",ActivityKind.Internal);
    string PayRetailersID = Guid.NewGuid().ToString();


    activity2?.SetTag("PayRetailersID", PayRetailersID);

    // var span1 = tracer.StartSpan("GeneratePaymentLog");
    // span1.AddEvent("Started generating payment log");
    
    // activity2.SetParentId(activity?.Context.TraceId.ToString());
    
    // Generate simulated payment log
    activity2?.AddEvent(new ActivityEvent("Starting Getting logs"));
    var paymentLog = GenerateSimulatedPaymentLog();
    // span1.AddEvent("Payment log generated successfully");
    activity2?.AddEvent(new ActivityEvent("Logs de test generados"));
    activity2?.Stop();

    using var activity3 = CustomTracer.StartActivity("SendLogsToElastic",ActivityKind.Internal);
    activity3?.AddEvent(new("Logging To Elastic"));
    // Log the generated payment log using Serilog
    Log.Information(paymentLog);  
    activity3?.AddEvent(new(paymentLog));
    activity2?.Stop();

    
    // Return the generated payment log as JSON response
    return Results.Json(paymentLog);
});

// Error handling middleware
app.UseExceptionHandler("/error"); // Redirect to /error in case of an exception

// Run the app
app.Run();

// Method to generate simulated payment log data
static string GenerateSimulatedPaymentLog()
{
    var paymentLog = new
    {
        State = (string)null,
        PdfUrl = (string)null,
        TypefulLine = (string)null,
        PaymentId = (string)null,
        PaymentGuid = (string)null,
        TrackingId = Guid.NewGuid().ToString(),
        PaymentStatusTypeCode = (string)null,
        PaymentPreviousStatusTypeCode = (string)null,
        ShopLegacyId = "12348459",
        ShopName = "(Test) Payments Processing Automation Shop",
        CreatedDate = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        PaymentMethod = "FitbankBoleto",
        PaymentMethodChannelCode = "ONLINE",
        NotificationUrl = "ASYNC:ONE:one-stg-core-topic-txnotifmsg",
        SuccessUrl = "https://pr-az-stg-one-core-internal-1.azurewebsites.net/v2/public/transactions/gateway/success/462ae6ae-bf30-4be8-9397-1ce40c8445a9",
        Language = "PT",
        Description = "PPD_Auto_Fitbank Boleto",
        Country = "BR",
        Currency = "BRL",
        Price = 6.0,
        ExpirationDate = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        ExpirationTime = 100,
        PaidDate = (string)null,
        CustomDomain = "payretailers.com",
        TestMode = false,
        PspExtraReference = (string)null,
        PaymentPspReference = (string)null,
        PaymentPspGateway = (string)null,
        PaymentPayerInfo = new
        {
            FirstName = "FirstNameTest",
            LastName = "LastNameTest",
            PersonalIdentification = "49207577000168",
            Email = "qa-test_483@payretailers.com",
            Phone = "192755174",
            AccountNumber = (string)null,
            MaskCardNumber = (string)null,
            Bank = (string)null,
            HolderCardName = (string)null,
            Address = "695 Ewing Pass",
            City = "Granite City",
            Zip = "37377"
        },
        PaymentWhitelabel = new
        {
            PrimaryColor = (string)null,
            AccentColor = (string)null,
            BackgroundColor = (string)null,
            ImageUrl = (string)null,
            IsTextButtonBlack = (string)null,
            BackgroundImageUrl = (string)null
        },
        SerializedIpnRequest = (string)null,
        PaymentPendingIpns = (string)null,
        PspPayload = (string)null,
        ExtraData = (string)null,
        id = (string)null
    };

    // Serialize the payment log object to JSON
    return System.Text.Json.JsonSerializer.Serialize(paymentLog);
}
