using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SemanticSearch.Application.AdditionalClasses;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Application.Services;
using SemanticSearch.Infrastructure.Data;
using SemanticSearch.Infrastructure.Repositories;
using SemanticSearch.Infrastructure.VectorStore;
using SemanticSearch.Web.Data;
using System.Text.Json.Serialization;

namespace SemanticSearch.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // MVC
            builder.Services.AddControllersWithViews();

            var connectionString = builder.Configuration.GetConnectionString("LocalConnection");

            // Контекст для Поиска (в Infrastructure)
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Контекст для Identity (в Web/Data)
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Настройка Identity 
            builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>() 
            .AddDefaultTokenProviders();

            // Memory Cache
            builder.Services.AddMemoryCache();

            builder.Services.AddSingleton<ISynonymApiService, SynonymApiService>();

            // Репозитории
            builder.Services.AddScoped<LinguisticRepository>();
            builder.Services.AddScoped<ParagraphRepository>();
            builder.Services.AddScoped<VectorRepository>();

            // Векторное хранилище
            builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();

            // Сервисы
            builder.Services.AddScoped<ILinguisticService, LinguisticService>();
            builder.Services.AddScoped<ISynonymApiService, SynonymApiService>();
            builder.Services.AddScoped<IRankingService, RankingService>();

            // Embedding Service
            var modelPath = Path.Combine(builder.Environment.ContentRootPath, "ml-models", "all-minilm-l6-v2");
            builder.Services.AddSingleton<IEmbeddingService>(sp => new EmbeddingService(
                sp.GetRequiredService<ILogger<EmbeddingService>>(), modelPath));

            // Главный сервис поиска
            builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
            


            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });
            var app = builder.Build();

            // Инициализация при старте (Ленивая загрузка модели)
            // Модель загрузится только при первом запросе поиска, чтобы не тормозить старт

            using (var scope = app.Services.CreateScope())
            {
                var linguisticService = scope.ServiceProvider.GetRequiredService<ILinguisticService>();
                await linguisticService.LoadDataAsync();
            }

            // Middleware
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
            
            app.MapControllers();

            using (var scope = app.Services.CreateScope())
            {
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var searchService = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();
                var linguisticService = scope.ServiceProvider.GetRequiredService<ILinguisticService>();

                // лингвистические данные
                await linguisticService.LoadDataAsync();

                //_logger.LogInformation("Pre-loading embedding model...");
                await embeddingService.InitializeAsync();

                // Индексируются все абзацы
                //_logger.LogInformation("Pre-indexing paragraphs...");
                await searchService.IndexPendingParagraphsAsync();

                //_logger.LogInformation("All services pre-loaded and ready!");
            }

            app.Run();
        }
    }
}