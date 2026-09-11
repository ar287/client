using SessionManagement.Server.Hubs;
using SessionManagement.Server.Services;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")!;

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Core services
builder.Services.AddSingleton(new AuthService(connectionString));
builder.Services.AddSingleton(new SessionService(connectionString));
builder.Services.AddSingleton(new BillingService(connectionString));
builder.Services.AddSingleton(new CustomerService(connectionString));
builder.Services.AddSingleton<AIService>();
builder.Services.AddSingleton(new LogService(connectionString));
builder.Services.AddSingleton(new SessionQueryService(connectionString));
builder.Services.AddSingleton(sp => new EventService(connectionString, sp.GetRequiredService<ILogger<EventService>>()));
builder.Services.AddSingleton(sp => new ClientMachineService(connectionString, sp.GetRequiredService<ILogger<ClientMachineService>>()));
builder.Services.AddSingleton(sp => new AnalyticsService(connectionString, sp.GetRequiredService<ILogger<AnalyticsService>>()));
builder.Services.AddSingleton(sp => new RuleEngineService(
    connectionString,
    sp.GetRequiredService<ILogger<RuleEngineService>>(),
    sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<AlertHub>>()));
builder.Services.AddSingleton(sp => new ApprovalService(connectionString, sp.GetRequiredService<ILogger<ApprovalService>>()));
builder.Services.AddSingleton(sp => new RemediationService(
    connectionString,
    sp.GetRequiredService<ILogger<RemediationService>>(),
    sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SessionHub>>()));
builder.Services.AddSingleton(sp => new AuditExportService(connectionString, sp.GetRequiredService<ILogger<AuditExportService>>()));

builder.Services.AddSingleton(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new ImageService(connectionString, env.WebRootPath);
});

builder.Services.AddScoped(sp =>
{
    var hub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SessionHub>>();
    return new SecurityService(connectionString, hub);
});

builder.Services.AddScoped(sp =>
{
    var hub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SessionHub>>();
    return new TerminationService(connectionString, hub);
});

builder.Services.AddHostedService<AutoTerminationBackgroundService>();

string serverUrls = builder.Configuration["Urls"] ?? "http://0.0.0.0:5102";
builder.WebHost.UseUrls(serverUrls);

var app = builder.Build();

// Wire SecurityService into AuthService
using (var scope = app.Services.CreateScope())
{
    var authService = app.Services
        .GetRequiredService<AuthService>();
    var securityService = scope.ServiceProvider
        .GetRequiredService<SecurityService>();
    authService.SetSecurityService(securityService);
}

app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SessionHub>("/sessionhub");
app.MapHub<AlertHub>("/alerthub");

PasswordSeeder.PrintHashes();

app.Run();
