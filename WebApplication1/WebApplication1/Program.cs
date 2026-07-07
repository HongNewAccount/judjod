using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=OrganizationDashboard;User=root;Password=1234;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var gregorianCulture = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = gregorianCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = gregorianCulture;

var app = builder.Build();
app.UseRequestLocalization("en-US");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.ToString().ToLower();
    var userId = context.Session.GetInt32("UserId");

    // endpoint สำหรับ JS เช็ค session (ใช้ใน pageshow bfcache guard)
    if (path == "/session/ping")
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        await context.Response.WriteAsync(userId.HasValue ? "true" : "false");
        return;
    }

    var isPublic = path.StartsWith("/auth") ||
                   path.StartsWith("/css") || path.StartsWith("/js") ||
                   path.StartsWith("/lib") || path.StartsWith("/images") ||
                   path.StartsWith("/uploads") ||
                   path == "/" || path == "";

    if (!isPublic && userId == null)
    {
        context.Response.Redirect("/Auth/Login");
        return;
    }

    if (!isPublic)
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }

    await next.Invoke();
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "catch-all",
    pattern: "{controller}/{action}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        context.Database.Migrate();
    }
    catch
    {
        context.Database.EnsureCreated();
    }

    SeedData.Initialize(context);
}

app.Run();
