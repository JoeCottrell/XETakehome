using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using RateAlerts.Api.Alerts;
using RateAlerts.Api.Rates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddOptions<XecdOptions>()
    .Bind(builder.Configuration.GetSection(XecdOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RateBoardOptions>()
    .Bind(builder.Configuration.GetSection(RateBoardOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Typed client: one pooled HttpClient with the credentials applied once, instead of a new client
// (and a new connection) per request.
builder.Services.AddHttpClient<XeRateProvider>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<XecdOptions>>().Value;
    var credentials = Convert.ToBase64String(
        Encoding.ASCII.GetBytes($"{options.AccountId}:{options.ApiKey}"));

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
});

// The cache holds state, so it is the singleton; the provider around it stays transient so it keeps
// using factory-managed HttpClient instances rather than pinning one forever.
builder.Services.AddSingleton<RateCache>();
builder.Services.AddTransient<IRateProvider>(provider => new CachingRateProvider(
    provider.GetRequiredService<XeRateProvider>(),
    provider.GetRequiredService<RateCache>(),
    provider.GetRequiredService<TimeProvider>(),
    provider.GetRequiredService<IOptions<XecdOptions>>()));

builder.Services.AddSingleton<IAlertStore, InMemoryAlertStore>();
builder.Services.AddTransient<IAlertService, AlertService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();

app.Run();
