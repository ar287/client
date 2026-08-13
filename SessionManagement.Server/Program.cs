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

builder.WebHost.UseUrls("http://localhost:5102");

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

PasswordSeeder.PrintHashes();

app.Run();
