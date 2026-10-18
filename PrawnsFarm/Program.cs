using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Persist Data Protection keys to disk so antiforgery tokens and auth cookies
// survive app-pool recycles on shared hosting (MonsterASP). Without this the
// keys live only in memory and are regenerated on every restart, which makes
// previously-issued login tokens fail validation with HTTP 400.
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("PrawnsFarm");

// Allow the Smart Insert screen to send the anti-forgery token via a header
// on its fetch() POST (JSON body).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ReportRepository>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Create DB (if missing), seed initial data, and install stored procedures
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    // Print where we are connecting (password is never logged) so startup
    // failures on the host are easy to diagnose from the stdout log.
    var rawCs = builder.Configuration.GetConnectionString("DefaultConnection");
    try
    {
        var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(rawCs);
        logger.LogInformation("DB target => Server={Server}; Database={Database}; User={User}",
            csb.DataSource, csb.InitialCatalog,
            string.IsNullOrEmpty(csb.UserID) ? "(integrated)" : csb.UserID);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DefaultConnection is missing or malformed. Value present: {HasValue}",
            !string.IsNullOrWhiteSpace(rawCs));
        throw;
    }

    try
    {
        logger.LogInformation("Starting database seeding...");
        await DbSeeder.SeedAsync(db);
        logger.LogInformation("Database seeding completed.");

        logger.LogInformation("Starting stored procedure installation...");
        await ProcedureInstaller.ApplyAsync(db, env, logger);
        logger.LogInformation("Stored procedure installation completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database startup (seed/stored-procedure install) FAILED. " +
            "Check the connection string, that the MonsterASP SQL database exists, and that the " +
            "SQL login has permission to CREATE TABLE / CREATE PROCEDURE.");
        throw;
    }
}

app.Run();
