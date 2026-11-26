using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SemanticSearch.Application.AdditionalClasses;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Application.Services;
using SemanticSearch.Application.ExternalSources;   // ? ДОБАВЛЕНО
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
            builder.Services.AddScoped<DocumentRepository>();        // ? ДОБАВЛЕНО (нужен DocumentService)

            // Векторное хранилище
            builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();

            // Сервисы
            builder.Services.AddScoped<ILinguisticService, LinguisticService>();
            builder.Services.AddScoped<ISynonymApiService, SynonymApiService>();
            builder.Services.AddScoped<IRankingService, RankingService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();   // ? ДОБАВЛЕНО

            // HTTP-клиенты с базовыми адресами
            builder.Services.AddHttpClient("openalex", client =>
            {
                client.BaseAddress = new Uri("https://api.openalex.org/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddHttpClient("semanticscholar", client =>
            {
                client.BaseAddress = new Uri("https://api.semanticscholar.org/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddHttpClient("arxiv", client =>
            {
                client.BaseAddress = new Uri("https://export.arxiv.org/");
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            builder.Services.AddHttpClient("europepmc", client =>
            {
                client.BaseAddress = new Uri("https://www.ebi.ac.uk/europepmc/webservices/rest/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddHttpClient("core", client =>
            {
                client.BaseAddress = new Uri("https://api.core.ac.uk/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Клиенты источников (оба попадут в IEnumerable<IScholarlySourceClient>)
            builder.Services.AddScoped<IScholarlySourceClient, OpenAlexClient>();
            builder.Services.AddScoped<IScholarlySourceClient, SemanticScholarClient>();
            builder.Services.AddScoped<IScholarlySourceClient, ArxivClient>();
            builder.Services.AddScoped<IScholarlySourceClient, EuropePmcClient>();
            builder.Services.AddScoped<IScholarlySourceClient, CoreClient>();

            // Сервис импорта: источники ? Document/Paragraph ? вектора
            builder.Services.AddScoped<ScholarlyImportService>();
            // ============================================================

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

                await linguisticService.LoadDataAsync();
                await embeddingService.InitializeAsync();
                await searchService.IndexPendingParagraphsAsync();
            }

            app.Run();
        }
    }
}