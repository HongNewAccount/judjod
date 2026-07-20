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

    if (path.StartsWith("/auth") ||
        path.StartsWith("/css") || path.StartsWith("/js") ||
        path.StartsWith("/lib") || path.StartsWith("/images") ||
        path == "/" || path == "")
    {
        await next.Invoke();
        return;
    }

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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    static void EnsureColumn(ApplicationDbContext dbContext, string tableName, string columnName, string columnDefinition)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = @tableName
  AND column_name = @columnName";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var columnParam = command.CreateParameter();
        columnParam.ParameterName = "@columnName";
        columnParam.Value = columnName;
        command.Parameters.Add(columnParam);

        dbContext.Database.OpenConnection();
        try
        {
            var exists = Convert.ToInt32(command.ExecuteScalar());
            if (exists == 0)
            {
                dbContext.Database.ExecuteSqlRaw($"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition}");
            }
        }
        finally
        {
            dbContext.Database.CloseConnection();
        }
    }

    // Patch missing columns BEFORE Migrate() so EF Core can read these tables
    try
    {
        EnsureColumn(context, "Projects", "Progress", "int NOT NULL DEFAULT 0");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Projects column patch failed: {ex.Message}");
    }

    try
    {
        EnsureColumn(context, "ChatMessages", "ImagePath", "varchar(500) NULL");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ChatMessages column patch failed: {ex.Message}");
    }

    try
    {
        context.Database.Migrate();
    }
    catch
    {
        context.Database.EnsureCreated();
    }

    try
    {
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `ProjectProgressLogs` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `ProjectId` int NOT NULL,
                `UserId` int NOT NULL,
                `Progress` int NOT NULL DEFAULT 0,
                `Note` longtext CHARACTER SET utf8mb4 NULL,
                `CreatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`),
                KEY `IX_ProjectProgressLogs_ProjectId` (`ProjectId`),
                KEY `IX_ProjectProgressLogs_UserId` (`UserId`),
                CONSTRAINT `FK_ProjectProgressLogs_Projects_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Projects` (`Id`) ON DELETE CASCADE,
                CONSTRAINT `FK_ProjectProgressLogs_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET utf8mb4;");
    }
    catch { }

    try
    {
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `ChatMessages` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `UserId` int NOT NULL,
                `IsFromAdmin` tinyint(1) NOT NULL DEFAULT 0,
                `Content` longtext CHARACTER SET utf8mb4 NOT NULL,
                `ImagePath` varchar(500) NULL,
                `IsRead` tinyint(1) NOT NULL DEFAULT 0,
                `CreatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`),
                KEY `IX_ChatMessages_UserId` (`UserId`),
                CONSTRAINT `FK_ChatMessages_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET utf8mb4;");
        context.Database.ExecuteSqlRaw(@"
            INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
            VALUES ('20260709000000_AddChatMessages', '8.0.0');");
    }
    catch { }

    try
    {
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `ProjectGroupAssignments` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `ProjectId` int NOT NULL,
                `GroupId` int NOT NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `UX_ProjectGroupAssignments` (`ProjectId`, `GroupId`),
                KEY `IX_ProjectGroupAssignments_GroupId` (`GroupId`),
                CONSTRAINT `FK_PGA_Projects` FOREIGN KEY (`ProjectId`) REFERENCES `Projects` (`Id`) ON DELETE CASCADE,
                CONSTRAINT `FK_PGA_Groups` FOREIGN KEY (`GroupId`) REFERENCES `ProjectGroups` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET utf8mb4;");

        // Migrate existing single GroupId → junction table
        context.Database.ExecuteSqlRaw(@"
            INSERT IGNORE INTO `ProjectGroupAssignments` (`ProjectId`, `GroupId`)
            SELECT `Id`, `GroupId` FROM `Projects`
            WHERE `GroupId` IS NOT NULL;");
    }
    catch { }

    SeedData.Initialize(context);
}

app.Run();
