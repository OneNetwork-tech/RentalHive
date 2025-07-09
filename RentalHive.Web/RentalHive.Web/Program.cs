using Microsoft.AspNetCore.Components.Authorization;
using RentalHive.Web.Auth;
using RentalHive.Web.Components;
using RentalHive.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpClient();

builder.Services.AddAuthentication(options =>
{
options.DefaultScheme = "CustomAuthScheme";
    options.DefaultChallengeScheme = "CustomAuthScheme";
})
    .AddCookie("CustomAuthScheme", options =>
    {
        options.LoginPath = "/login"; // Path to your login page
        options.LogoutPath = "/logout"; // Path to your logout page
        options.AccessDeniedPath = "/access-denied"; // Path for access denied
    });



//builder.Services.AddAuthentication("CustomAuth");

// 2. Add Blazor's core Authorization services.
builder.Services.AddAuthorizationCore();

// 3. Register our custom AuthenticationStateProvider as the primary one.
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<CustomAuthenticationStateProvider>());

// --- FIX ENDS HERE ---

// 4. Register our custom AuthService for handling login/logout logic.
builder.Services.AddScoped<IAuthService, AuthService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// IMPORTANT: Add authentication and authorization middleware to the pipeline.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RentalHive.Web.Client._Imports).Assembly);

app.Run();
