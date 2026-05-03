using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SemanticSearch.Application.IServices;
using SemanticSearch.Application.Services;
using SemanticSearch.Infrastructure.Data;
using SemanticSearch.Infrastructure.Repositories;
using SemanticSearch.Web.Data;

namespace SemanticSearch.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllersWithViews();

            // DB Context
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Identity DbContext
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Identity configuration
            builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Repositories
            builder.Services.AddScoped<LinguisticRepository>();

            // Memory Cache
            builder.Services.AddMemoryCache();

            // Services
            builder.Services.AddScoped<ILinguisticService, AdvancedLinguisticService>();

            // SearchService с кэшированием
            builder.Services.AddScoped<ISearchService>(sp =>
                new CachedSearchService(
                    new AdvancedSearchService(
                        sp.GetRequiredService<LinguisticRepository>(),
                        sp.GetRequiredService<ILinguisticService>()
                    ),
                    sp.GetRequiredService<IMemoryCache>()
                )
            );

            // Настройка пула потоков
            ThreadPool.SetMinThreads(10, 10);

            // Build the app
            var app = builder.Build();

            // Initialize Linguistic Data on Startup
            using (var scope = app.Services.CreateScope())
            {
                var linguisticService = scope.ServiceProvider.GetRequiredService<ILinguisticService>();
                var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

                await linguisticService.LoadDataAsync();
                await searchService.SearchAsync(""); 
            }

            // Configure the HTTP request pipeline
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
                pattern: "{controller=Search}/{action=Index}/{id?}");

            app.Run();
        }
    }
}