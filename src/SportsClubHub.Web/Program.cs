using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SportsClubHub.Web.Components;
using SportsClubHub.Web.Data;
using SportsClubHub.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
// The connection string holds only a file path (SQLite has no user name or password).
// A relative path is placed under the project folder so it works from any working directory.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=App_Data/sportsclubhub.db";
const string sqlitePrefix = "Data Source=";
if (connectionString.StartsWith(sqlitePrefix, StringComparison.OrdinalIgnoreCase))
{
    var databaseFile = connectionString[sqlitePrefix.Length..];
    if (!Path.IsPathRooted(databaseFile))
    {
        databaseFile = Path.Combine(builder.Environment.ContentRootPath, databaseFile);
    }

    Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
    connectionString = sqlitePrefix + databaseFile;
}

// A DbContext factory is the recommended pattern for Blazor Server: each operation creates a
// short-lived DbContext, so two things happening at once never share one context.
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

// ---------- Application services ----------
builder.Services.AddScoped<SportService>();
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<VenueService>();
builder.Services.AddScoped<ParticipantInterestService>();
builder.Services.AddScoped<InsightsService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<AdminAccountService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddSingleton<EmailService>();

// ---------- Authentication and authorisation ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SportsClubHub.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ---------- Blazor ----------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

var app = builder.Build();

// Create the database (never deleting existing data) and add starter data.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
