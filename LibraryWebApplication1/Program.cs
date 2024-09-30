using LibraryWebApplication1.Models;
using LibraryWebApplication1;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Azure.Messaging.ServiceBus;
using LibraryWebApplication1.Services;
using Azure.Storage.Blobs;
var builder = WebApplication.CreateBuilder(args);
var blobConnectionString = builder.Configuration["AzureBlobStorage:ConnectionString"];
var blobContainerName = builder.Configuration["AzureBlobStorage:ContainerName"];
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<DblibraryContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<DblibraryContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = "Application";
    o.DefaultSignInScheme = "External";
})
            .AddCookie("Application")
            .AddCookie("External")
            .AddGoogle(o =>
            {
                o.ClientId = "755861786405-vp3j3666mmhr8l3outj8lm70jnu1bjh3.apps.googleusercontent.com";
                o.ClientSecret = "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";
            });
builder.Services.AddSingleton<LuceneService>();
builder.Services.AddSingleton<ServiceBusClient>(provider =>
{
    string connectionString = builder.Configuration.GetConnectionString("ServiceBusConnection");
    return new ServiceBusClient(connectionString);
});
builder.Services.AddSingleton<FileProcessingService>();
builder.Services.AddSingleton<ImageProcessingService>();
builder.Services.AddSingleton(x => new BlobServiceClient(blobConnectionString));
builder.Services.AddHostedService<FileProcessingBackgroundService>(provider =>
{
    var serviceBusClient = provider.GetRequiredService<ServiceBusClient>();
    var logger = provider.GetRequiredService<ILogger<FileProcessingBackgroundService>>();
    var imageProcessingService = provider.GetRequiredService<ImageProcessingService>();
    var webHostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();

    return new FileProcessingBackgroundService(serviceBusClient, "queue", logger, imageProcessingService, webHostEnvironment);
});
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DblibraryContext>();
        var luceneService = services.GetRequiredService<LuceneService>();
        luceneService.ClearIndex();
        var users = context.Users.ToList();
        var categories = context.Categories.ToList();
        var articles = context.Articles.ToList();
        luceneService.AddAllToIndex(categories,articles,users);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}
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
    pattern: "{controller=Login}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "usersMap",
    pattern: "{controller=Users}/{action=UsersMap}");
app.MapControllerRoute(
    name: "blob",
    pattern: "{controller=Blob}/{action=UploadForm}/{id?}");
app.Run();

