using API.Extentions;
using API.Middleware;
using API.SignalR;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Persistence;

var builder = WebApplication.CreateBuilder(args);

// Enforce HTTPS
builder.Services.AddHttpsRedirection(options => {
    options.HttpsPort = 5000; // Your HTTPS port
});

builder.WebHost.ConfigureKestrel(serverOptions => {
    serverOptions.ListenLocalhost(5000, listenOptions => {
        listenOptions.UseHttps();
    });
    // Remove HTTP if not needed
    // serverOptions.ListenLocalhost(5001); 
});


Console.WriteLine("### LOADED CONFIGURATION ###");
foreach (var config in builder.Configuration.AsEnumerable())
{
    Console.WriteLine($"{config.Key}: {config.Value}");
}

// Add services to the container.

builder.Services.AddControllers(opt =>
{
    var policy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    opt.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseXContentTypeOptions();
app.UseReferrerPolicy(opt => opt.NoReferrer());
app.UseXXssProtection(opt=>opt.EnabledWithBlockMode());
app.UseXfo(opt => opt.Deny());
app.UseCsp(opt => opt
    .BlockAllMixedContent()
    .StyleSources(s => s.Self()
        .CustomSources("https://fonts.googleapis.com")
        .UnsafeInline())
    .FontSources(s => s.Self()
        .CustomSources("https://fonts.gstatic.com", "data:"))
    .FormActions(s => s.Self())
    .FrameAncestors(s => s.Self())
    .ImageSources(s => s.Self()
        .CustomSources(
            "blob:",
            "data:",
            "https://res.cloudinary.com",
            "https://platform-lookaside.fbsbx.com", // Facebook's CDN for profile images
            "https://*.facebook.com" // Wildcard for all Facebook domains
        ))
    .ScriptSources(s => s.Self()
        .CustomSources(
            "https://connect.facebook.net", // Facebook SDK
            "https://*.facebook.com" // Additional Facebook domains
        ))
    .ConnectSources(s => s.Self()
        .CustomSources(
            "https://graph.facebook.com", // For API calls
            "https://*.facebook.com"
        ))
);
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.Use(async (context, next) =>
    {
       context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000");
       await next.Invoke();
    });
}
app.UseCors("CorsPolicy");

app.UseAuthentication();

app.UseAuthorization();



app.MapControllers();
app.MapHub<ChatHub>("/chat");
app.MapFallbackToController("Index", "Fallback");

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    var context = services.GetRequiredService<DataContext>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    await context.Database.MigrateAsync();
    await Seed.SeedData(context,userManager);
}
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occured during migration");
}

app.Run();
