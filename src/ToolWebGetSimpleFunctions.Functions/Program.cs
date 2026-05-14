using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ToolWebGetSimpleFunctions.Functions.Options;
using ToolWebGetSimpleFunctions.Functions.Services;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services
            .AddOptions<ApplicationOptions>()
            .Bind(context.Configuration.GetSection(ApplicationOptions.SectionName));

        services
            .AddOptions<SourceCollectionOptions>()
            .Bind(context.Configuration.GetSection(SourceCollectionOptions.SectionName));

        services
            .AddOptions<AzureOpenAIOptions>()
            .Bind(context.Configuration.GetSection(AzureOpenAIOptions.SectionName));

        services.AddHttpClient("source")
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<SourceCollectionOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            });

        services.AddHttpClient("aoai")
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IHtmlTextExtractor, AngleSharpHtmlTextExtractor>();
        services.AddSingleton<ISourceCollector, RakuSpaSourceCollector>();
        services.AddSingleton<IAzureOpenAIExtractionClient, AzureOpenAIRestExtractionClient>();
        services.AddSingleton<IEventCandidateValidator, EventCandidateValidator>();
        services.AddSingleton<IEventSelectionService, EventSelectionService>();
        services.AddSingleton<IRakuSpaEventLookupService, RakuSpaEventLookupService>();
    })
    .Build();

host.Run();
