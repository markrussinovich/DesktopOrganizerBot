using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Chatbot;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        //Config and add open telemetry for logging and tracing
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        }).SetMinimumLevel(LogLevel.Trace);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Microsoft.SemanticKernel*");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("Microsoft.SemanticKernel*")
                    .AddSource("DesktopOrganizerBot");
            })
            .UseOtlpExporter();

        var a = Assembly.GetExecutingAssembly();
        using var stream = a.GetManifestResourceStream($"{a.GetName().Name}.Resources.appsettings.json")!;
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();

        AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);
        
        // Config chat endpoints for Semantic Kernel
        builder.Services
            .AddKernel()
            .AddAzureOpenAIChatCompletion(config["ModelDeployment"]!, config["AOAIEndpoint"]!, config["AOAIKey"]!)
            .AddOpenAIChatCompletion("phi-3-mini-q4", new Uri("http://localhost:5272/v1/chat/completions"), null, serviceId: "localmodel")
            .Plugins.AddFromType<OrganizeDesktopPlugin>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<ChatHistoryViewModel>();
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<ChatManager>();
        builder.Services.ConfigureHttpClientDefaults(c => c.ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan));

        MauiApp app = builder.Build();

        // Hack workaround for metrics/traces from MAUI not being sent
        Task.WhenAll(from s in app.Services.GetServices<IHostedService>() select s.StartAsync(default)).Wait();

        return app;
    }
}