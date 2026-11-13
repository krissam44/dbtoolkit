using SqliteToolkit;
using SqliteToolkit.Demo.Components;
using SqliteToolkit.Demo.Data;
using SqliteToolkit.Demo.Repositories;
using SqliteToolkit.Demo.Services;
using SqliteToolkit.Migrations;
using System.Reflection;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "Data"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var databaseSection = builder.Configuration.GetSection("Database");

builder.Services.AddScoped<DatabaseConnectionTestService>();

builder.Services.AddSqliteToolkit(options =>
{
    options.ConnectionString =
        databaseSection["ConnectionString"] ?? string.Empty;

    options.DefaultTimeout =
        databaseSection.GetValue<int>("DefaultTimeout", 30);

    options.ForeignKeys =
        databaseSection.GetValue<bool>("ForeignKeys", true);
});

builder.Services.AddScoped<ClientRepository>();
builder.Services.AddScoped<ClientService>();

builder.Services.AddMudServices();

builder.Services.AddScoped<ErrorTestRepository>();
builder.Services.AddScoped<UnitOfWorkTestService>();

var app = builder.Build();

/*
using (var scope = app.Services.CreateScope())
{    var migrationRunner = scope.ServiceProvider
        .GetRequiredService<MigrationRunner>();

    var assembly = Assembly.GetExecutingAssembly();

    foreach (var resource in assembly.GetManifestResourceNames())
    {
        Console.WriteLine($"RESSOURCE : {resource}");
    }

    await migrationRunner.MigrateAsync(
        Assembly.GetExecutingAssembly(),
        "SqliteToolkit.Demo.Migrations.Sql");
}
*/

using (var scope = app.Services.CreateScope())
{
    var migrationRunner = scope.ServiceProvider
        .GetRequiredService<MigrationRunner>();

    var assembly = Assembly.GetExecutingAssembly();

    app.Logger.LogWarning("=== DEBUT DES RESSOURCES ===");

    foreach (var resource in assembly.GetManifestResourceNames())
    {
        app.Logger.LogWarning("RESSOURCE : {Resource}", resource);
    }

    await migrationRunner.MigrateAsync(
        assembly,
        "SqliteToolkit.Demo.Migrations.Sql");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
