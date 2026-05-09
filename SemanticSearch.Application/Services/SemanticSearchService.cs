using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.Helpers;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Entities;
using SemanticSearch.Core.Enums;
using SemanticSearch.Infrastructure.Repositories;
using SemanticSearch.Infrastructure.VectorStore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IRankingService _rankingService;
        private readonly ILinguisticService _linguisticService;
        private readonly ParagraphRepository _paragraphRepo;
        private readonly VectorRepository _vectorRepo;
        private readonly IVectorStore _vectorStore;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SemanticSearchService> _logger;

        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public SemanticSearchService(
            IEmbeddingService embeddingService,
            IRankingService rankingService,
            ILinguisticService linguisticService,
            ParagraphRepository paragraphRepo,
            VectorRepository vectorRepo,
            IVectorStore vectorStore,
            IMemoryCache cache,
            ILogger<SemanticSearchService> logger)
        {
            _embeddingService = embeddingService;
            _rankingService = rankingService;
            _linguisticService = linguisticService;
            _paragraphRepo = paragraphRepo;
            _vectorRepo = vectorRepo;
            _vectorStore = vectorStore;
            _cache = cache;
            _logger = logger;
        }

        public async Task<SearchResponseDto> SearchAsync(SearchRequestDto request)
        {
            var stopwatch = Stopwatch.StartNew();
            var threadId = Thread.CurrentThread.ManagedThreadId;

            _logger?.LogInformation($"Search started on thread {threadId}: '{request.Query}'");
            var response = new SearchResponseDto
            {
                Query = request.Query,
                AlgorithmUsed = request.Algorithm.ToString()
            };

            // Кэширование
            if (request.UseCache)
            {
                var cacheKey = $"search_{request.Algorithm}_{request.Query.ToLower().Trim()}";

                var cached = _cache.Get<SearchResponseDto>(cacheKey);
                if (cached != null)
                {
                    cached.FromCache = true;
                    return cached;
                }
            }

            // Инициализация при первом запуске
            if (!_isInitialized)
            {
                await _initLock.WaitAsync();
                try
                {
                    if (!_isInitialized)
                    {
                        await InitializeAsync();
                        _isInitialized = true;
                    }
                }
                finally
                {
                    _initLock.Release();
                }
            }

            // Генерация вектора запроса (для векторного поиска)
            float[]? queryVector = null;
            var vectorSearchTime = 0;

            if (request.Algorithm is SearchAlgorithm.Vector or SearchAlgorithm.Hybrid or SearchAlgorithm.HybridSemantic)
            {
                var vectorStopwatch = Stopwatch.StartNew();
                queryVector = await _embeddingService.GenerateEmbeddingAsync(request.Query);
                vectorSearchTime = (int)vectorStopwatch.ElapsedMilliseconds;
            }

            // Загрузка абзацев
            var paragraphs = await _paragraphRepo.GetAllWithVectorsAsync();
            var activeParagraphs = paragraphs
                .Where(p => p.Document?.IsActive == true)
                .ToList();

            // Загрузка векторов в память для поиска
            foreach (var p in activeParagraphs)
            {
                if (p.Vector?.VectorData != null && p.Embedding == null)
                {
                    p.Embedding = VectorMath.BytesToFloats(p.Vector.VectorData);
                }
            }

            // Ранжирование
            var keywordSearchTime = 0;
            var keywordStopwatch = Stopwatch.StartNew();

            var ranked = await _rankingService.RankAsync(
                request.Query,
                activeParagraphs,
                queryVector ?? Array.Empty<float>(),
                request.Algorithm);

            keywordSearchTime = (int)keywordStopwatch.ElapsedMilliseconds;

            // Формирование результатов
            response.Matches = ranked
                .Take(request.TopK)
                .Select((r, idx) => new SearchMatchDto
                {
                    ParagraphId = r.Paragraph.Id,
                    DocumentId = r.Paragraph.DocumentId,
                    DocumentTitle = r.Paragraph.Document?.Title ?? "Unknown",
                    ParagraphContent = r.Paragraph.Content,

                    // 🔥 ПРОСТОЕ умножение на 100 для отображения
                    RelevanceScore = Math.Round(r.TotalScore * 100, 2),
                    VectorScore = Math.Round(r.VectorScore * 100, 2),
                    KeywordScore = 0,

                    Rank = idx + 1,
                    HighlightedWords = new List<string>(),
                    ScoreBreakdown = new Dictionary<string, double>
                    {
                        ["vector"] = r.TotalScore
                    }
                })
                .ToList();

            response.TotalResults = response.Matches.Count;
            response.VectorSearchTimeMs = vectorSearchTime;
            response.KeywordSearchTimeMs = keywordSearchTime;

            stopwatch.Stop();
            _logger?.LogInformation($"Search completed in {stopwatch.ElapsedMilliseconds}ms on thread {Thread.CurrentThread.ManagedThreadId}");
            response.TotalTimeMs = (int)stopwatch.ElapsedMilliseconds;

            // Кэширование результата
            if (request.UseCache && request.LogQuery)
            {
                var cacheKey = $"search_{request.Algorithm}_{request.Query.ToLower().Trim()}";
                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
            }

            // Логирование запроса
            if (request.LogQuery)
            {
                await LogQueryAsync(request, response);
            }

            _logger.LogInformation($"Search completed: {request.Query} → {response.Matches.Count} results in {response.TotalTimeMs}ms");

            return response;
        }

        private async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing semantic search service...");

            // Инициализация эмбеддингов
            await _embeddingService.InitializeAsync();

            // Инициализация векторного хранилища
            await _vectorStore.InitializeAsync(_embeddingService.VectorDimension);

            // Загрузка лингвистических данных
            await _linguisticService.LoadDataAsync();

            // Загрузка абзацев и инициализация статистики для BM25
            var paragraphs = await _paragraphRepo.GetAllWithVectorsAsync();
            (_rankingService as RankingService)?.InitializeStats(paragraphs);

            // Предзагрузка векторов в InMemoryVectorStore
            var vectors = await _vectorRepo.GetAllWithParagraphsAsync();
            _logger.LogInformation($"Loading {vectors.Count()} vectors from DB");

            foreach (var v in vectors)
            {
                try
                {
                    var floatVector = VectorMath.BytesToFloats(v.VectorData);

                    if (floatVector.Length != _embeddingService.VectorDimension)
                    {
                        _logger.LogWarning($"Skipping vector for paragraph {v.ParagraphId}: expected {_embeddingService.VectorDimension}, got {floatVector.Length}");
                        continue;
                    }

                    await _vectorStore.AddVectorAsync(v.ParagraphId, floatVector);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to load vector for paragraph {v.ParagraphId}");
                }
            }

            var pendingCount = paragraphs.Count(p => p.IndexedAt == null);
            if (pendingCount > 0)
            {
                _logger.LogInformation($"Found {pendingCount} paragraphs without vectors. Indexing...");
                await IndexPendingParagraphsAsync();
            }

            _logger.LogInformation("Semantic search service initialized");
        }

        public async Task<int> IndexPendingParagraphsAsync()
{
    var pending = await _paragraphRepo.GetNotIndexedAsync();
    var indexed = 0;

    _logger?.LogInformation($"Indexing {pending.Count()} pending paragraphs...");

    foreach (var paragraph in pending)
    {
        try
        {
            _logger?.LogDebug($"Generating embedding for paragraph {paragraph.Id} (length: {paragraph.Content.Length})");
            
            // Генерация эмбеддинга
            var embedding = await _embeddingService.GenerateEmbeddingAsync(paragraph.Content);
            
            _logger?.LogDebug($"Embedding generated: dim={embedding.Length}, first3=[{string.Join(", ", embedding.Take(3))}]");
            
            // Сохранение в БД
            var vectorEntity = new ParagraphVector
            {
                ParagraphId = paragraph.Id,
                VectorData = VectorMath.FloatsToBytes(embedding),
                VectorDimension = embedding.Length,
                ModelName = _embeddingService.ModelName,
                Normalized = true
            };
            
            await _vectorRepo.AddAsync(vectorEntity);
            await _paragraphRepo.MarkAsIndexedAsync(paragraph.Id);
            await _vectorStore.AddVectorAsync(paragraph.Id, embedding);
            
            paragraph.Embedding = embedding;
            
            indexed++;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to index paragraph {paragraph.Id}");
        }
    }

    _logger?.LogInformation($"Indexed {indexed} paragraphs");
    return indexed;
}

        public async Task<bool> ReindexParagraphAsync(int paragraphId)
        {
            var paragraph = await _paragraphRepo.GetByIdAsync(paragraphId);
            if (paragraph == null)
                return false;

            // Удаление старого вектора
            var existingVector = await _vectorRepo.GetByParagraphIdAsync(paragraphId);
            if (existingVector != null)
            {
                _vectorRepo.Remove(existingVector);
                await _vectorStore.RemoveVectorAsync(paragraphId);
            }

            // Генерация нового
            var embedding = await _embeddingService.GenerateEmbeddingAsync(paragraph.Content);

            var vectorEntity = new ParagraphVector
            {
                ParagraphId = paragraph.Id,
                VectorData = VectorMath.FloatsToBytes(embedding),
                VectorDimension = embedding.Length,
                ModelName = _embeddingService.ModelName,
                Normalized = true
            };

            await _vectorRepo.AddAsync(vectorEntity);
            await _paragraphRepo.MarkAsIndexedAsync(paragraph.Id);
            await _vectorStore.AddVectorAsync(paragraph.Id, embedding);

            paragraph.Embedding = embedding;
            paragraph.IndexedAt = DateTime.UtcNow;

            return true;
        }

        public async Task<IndexingStatsDto> GetIndexingStatsAsync()
        {
            var total = await _paragraphRepo.CountAsync();
            var indexed = await _vectorRepo.GetIndexedCountAsync();

            return new IndexingStatsDto
            {
                TotalParagraphs = total,
                IndexedParagraphs = indexed,
                PendingParagraphs = total - indexed,
                ModelName = _embeddingService.ModelName,
                VectorDimension = _embeddingService.VectorDimension
            };
        }

        private async Task LogQueryAsync(SearchRequestDto request, SearchResponseDto response)
        {
            // В реальном приложении: сохранить в БД через SearchQueryLog repository
            await Task.CompletedTask; // Заглушка
        }
    }
}