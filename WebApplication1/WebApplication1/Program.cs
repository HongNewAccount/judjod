using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure MySQL Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=OrganizationDashboard;User=root;Password=1234;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

// Configure session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthorization();

// Middleware to require login for all pages except Auth and static files
app.Use(async (context, next) =>
{
    var path = context.Request.Path.ToString().ToLower();
    var userId = context.Session.GetInt32("UserId");

    // Allow Auth, static files, and home without login
    if (path.StartsWith("/auth") ||
        path.StartsWith("/css") || path.StartsWith("/js") ||
        path.StartsWith("/lib") || path.StartsWith("/images") ||
        path == "/" || path == "")
    {
        await next.Invoke();
        return;
    }

    // Redirect to login if not authenticated
    if (userId == null)
    {
        context.Response.Redirect("/Auth/Login");
        return;
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

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Apply pending migrations
        context.Database.Migrate();
    }
    catch
    {
        // If migration fails, create database from scratch
        context.Database.EnsureCreated();
    }

    // Seed initial data
    SeedData.Initialize(context);
}

app.Run();
