using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SemanticSearch.Application.Helpers;
using SemanticSearch.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    /// <summary>
    /// Сервис генерации эмбеддингов через SBERT (ONNX)
    /// Работает без внешних токенизаторов - использует простой fallback
    /// </summary>
    public class EmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly ILogger<EmbeddingService> _logger;
        private readonly string _modelPath;

        private InferenceSession? _session;
        private SimpleTokenizer? _tokenizer;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public int VectorDimension => 384;
        public string ModelName => "all-MiniLM-L6-v2";
        public bool IsReady => _isInitialized && _session != null;

        public EmbeddingService(ILogger<EmbeddingService> logger, string modelPath)
        {
            _logger = logger;
            _modelPath = modelPath;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                _logger.LogInformation($"Loading SBERT model from {_modelPath}");

                if (!Directory.Exists(_modelPath))
                    throw new DirectoryNotFoundException($"Model directory not found: {_modelPath}");

                // Поиск ONNX модели
                var onnxFile = FindModelFile();
                if (string.IsNullOrEmpty(onnxFile))
                {
                    _logger.LogError("ONNX model not found. Please download model.onnx from HuggingFace");
                    _logger.LogWarning("URL: https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/tree/main/onnx");
                    return;
                }

                _logger.LogInformation($"Loading ONNX model: {onnxFile}");

                // Инициализация ONNX Runtime
                var sessionOptions = new SessionOptions
                {
                    InterOpNumThreads = 1,
                    IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2)
                };
                sessionOptions.AppendExecutionProvider_CPU(0);

                _session = new InferenceSession(onnxFile, sessionOptions);
                _logger.LogInformation("ONNX session created");

                // Инициализация простого токенизатора
                _tokenizer = new SimpleTokenizer(_modelPath);
                _logger.LogInformation("Tokenizer initialized");

                _isInitialized = true;
                _logger.LogInformation($"✓ Model ready. Dimension: {VectorDimension}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize embedding model");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private string? FindModelFile()
        {
            var candidates = new[]
            {
                Path.Combine(_modelPath, "onnx", "model.onnx"),
                Path.Combine(_modelPath, "model.onnx"),
                Path.Combine(_modelPath, "onnx", "model_quantized.onnx"),
                Path.Combine(_modelPath, "pytorch_model.bin")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (!IsReady)
                await InitializeAsync();

            if (string.IsNullOrWhiteSpace(text))
                return new float[VectorDimension];

            var embeddings = await GenerateEmbeddingsAsync(new[] { text });
            return embeddings.FirstOrDefault() ?? new float[VectorDimension];
        }

        public async Task<float[][]> GenerateEmbeddingsAsync(string[] texts)
        {
            if (!IsReady)
                await InitializeAsync();

            if (texts == null || texts.Length == 0)
                return Array.Empty<float[]>();

            var results = new float[texts.Length][];
            var batchSize = 4;

            for (int i = 0; i < texts.Length; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToArray();
                var batchResults = await ProcessBatchAsync(batch);

                for (int j = 0; j < batch.Length && i + j < results.Length; j++)
                {
                    results[i + j] = batchResults[j];
                }
            }

            return results;
        }

        private async Task<float[][]> ProcessBatchAsync(string[] texts)
        {
            _logger.LogInformation($"Processing batch on Thread {Thread.CurrentThread.ManagedThreadId}");
            return await Task.Run(() =>
            {
                if (_session == null || _tokenizer == null)
                    throw new InvalidOperationException("Model or tokenizer not initialized");

                // Токенизация
                var encoded = _tokenizer.EncodeBatch(texts);

                // Создание тензоров с long
                var inputIdsTensor = CreateLongTensor(encoded.InputIds, encoded.MaxLength);
                var attentionMaskTensor = CreateLongTensor(encoded.AttentionMask, encoded.MaxLength);
                var tokenTypeIdsTensor = CreateLongTensor(encoded.TokenTypeIds, encoded.MaxLength);

                // Подготовка входов для ONNX
                var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

                // Инференс
                using var results = _session.Run(inputs);

                // Извлечение эмбеддингов
                var embeddings = ExtractEmbeddings(results, texts.Length);

                // Нормализация
                for (int i = 0; i < embeddings.Length; i++)
                {
                    embeddings[i] = VectorMath.Normalize(embeddings[i]);
                }

                return embeddings;
            });
        }
        private DenseTensor<long> CreateLongTensor(int[][] values, int maxLength)
        {
            var tensor = new DenseTensor<long>(new[] { values.Length, maxLength });

            for (int b = 0; b < values.Length; b++)
            {
                for (int i = 0; i < values[b].Length && i < maxLength; i++)
                {
                    tensor[b, i] = values[b][i]; // Автоматическое преобразование int в long
                }
            }

            return tensor;
        }

        private float[][] ExtractEmbeddings(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int batchSize)
        {
            var output = results.FirstOrDefault(r =>
                r.Name == "sentence_embedding" ||
                r.Name == "last_hidden_state" ||
                r.Name == "token_embeddings");

            if (output == null)
            {
                foreach (var r in results)
                {
                    try
                    {
                        var t = r.AsTensor<float>();
                        if (t != null && t.Dimensions.Length >= 2)
                        {
                            output = r;
                            break;
                        }
                    }
                    catch { continue; }
                }
            }

            if (output == null)
                throw new InvalidOperationException("Could not find a valid output tensor in ONNX results.");

            // тензор и его размерности
            var tensor = output.AsTensor<float>();
            var dims = tensor.Dimensions; // Теперь это работает корректно

            // Обработка разных форматов выхода
            if (dims.Length == 2 && dims[0] == batchSize)
            {
                // [batch, hidden] - прямой эмбеддинг предложения
                return ExtractDirectEmbeddings(tensor, batchSize, (int)dims[1]);
            }
            else if (dims.Length == 3)
            {
                // [batch, seq, hidden] - нужен mean pooling
                return ExtractWithMeanPooling(tensor, batchSize, (int)dims[1], (int)dims[2]);
            }

            throw new InvalidOperationException($"Unexpected output shape: [{dims.ToString()}]");
        }

        private float[][] ExtractDirectEmbeddings(Tensor<float> tensor, int batchSize, int hiddenSize)
        {
            var embeddings = new float[batchSize][];

            for (int b = 0; b < batchSize; b++)
            {
                embeddings[b] = new float[hiddenSize];
                for (int h = 0; h < hiddenSize; h++)
                {
                    embeddings[b][h] = tensor[b, h];
                }
            }

            return embeddings;
        }

        private float[][] ExtractWithMeanPooling(Tensor<float> tensor, int batchSize, int seqLen, int hiddenSize)
        {
            var embeddings = new float[batchSize][];

            for (int b = 0; b < batchSize; b++)
            {
                embeddings[b] = new float[hiddenSize];
                int count = 0;

                for (int s = 0; s < seqLen; s++)
                {
                    // Пропуск паддинг токенов (эвристика)
                    var firstHidden = tensor[b, s, 0];
                    if (firstHidden == 0 && s > 0)
                        continue;

                    for (int h = 0; h < hiddenSize; h++)
                    {
                        embeddings[b][h] += tensor[b, s, h];
                    }
                    count++;
                }

                // Усреднение
                if (count > 0)
                {
                    for (int h = 0; h < hiddenSize; h++)
                    {
                        embeddings[b][h] /= count;
                    }
                }
            }

            return embeddings;
        }

        public void Dispose()
        {
            _session?.Dispose();
            _tokenizer?.Dispose();
            _initLock?.Dispose();
        }
    }

    /// <summary>
    /// Простой токенизатор для SBERT моделей
    /// Не требует внешних зависимостей - работает на основе базовых правил
    /// </summary>
    public class SimpleTokenizer : IDisposable
    {
        private readonly Dictionary<string, int> _vocab;
        private readonly int _maxSequenceLength;
        private readonly int _padTokenId;
        private readonly int _clsTokenId;
        private readonly int _sepTokenId;
        private readonly int _unkTokenId;

        public SimpleTokenizer(string modelPath, int maxSequenceLength = 128)
        {
            _maxSequenceLength = maxSequenceLength;
            _padTokenId = 0;
            _clsTokenId = 101;  // [CLS] для BERT-подобных моделей
            _sepTokenId = 102;  // [SEP]
            _unkTokenId = 100;  // [UNK]

            // Попытка загрузить vocab из разных возможных файлов
            _vocab = LoadVocab(modelPath) ?? CreateMinimalVocab();
        }

        private Dictionary<string, int>? LoadVocab(string modelPath)
        {
            var vocabPaths = new[]
            {
                Path.Combine(modelPath, "vocab.txt"),
                Path.Combine(modelPath, "tokenizer.json"),
                Path.Combine(modelPath, "vocab.json")
            };

            foreach (var path in vocabPaths)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    if (path.EndsWith(".txt"))
                        return LoadVocabTxt(path);
                    else if (path.EndsWith(".json"))
                        return LoadVocabJson(path);
                }
                catch
                {
                }
            }

            return null;
        }

        private Dictionary<string, int> LoadVocabTxt(string path)
        {
            var vocab = new Dictionary<string, int>();
            var lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length && i < 30000; i++)
            {
                var token = lines[i].Trim();
                if (!string.IsNullOrEmpty(token))
                    vocab[token] = i;
            }

            return vocab;
        }

        private Dictionary<string, int> LoadVocabJson(string path)
        {
            // Упрощённый парсер для vocab.json
            var vocab = new Dictionary<string, int>();
            var content = File.ReadAllText(path);

            // Простой regex для извлечения "токен": номер
            var matches = Regex.Matches(content, @"""([^""]+)""\s*:\s*(\d+)");

            foreach (Match match in matches)
            {
                if (vocab.Count >= 30000) break;
                vocab[match.Groups[1].Value] = int.Parse(match.Groups[2].Value);
            }

            return vocab;
        }

        private Dictionary<string, int> CreateMinimalVocab()
        {
            // Минимальный словарь для работы без vocab файла
            var vocab = new Dictionary<string, int>();

            // Special tokens
            vocab["[PAD]"] = 0;
            vocab["[UNK]"] = 100;
            vocab["[CLS]"] = 101;
            vocab["[SEP]"] = 102;
            vocab["[MASK]"] = 103;

            var commonRu = new[] { "а", "и", "в", "не", "на", "с", "к", "по", "что", "как",
                                   "это", "тот", "быть", "он", "она", "оно", "мы", "вы", "они",
                                   "токен", "текст", "слово", "модель", "нейрон", "сеть", "данные" };

            int id = 1000;
            foreach (var token in commonRu)
            {
                vocab[token] = id++;
                vocab[token.ToLower()] = id++;
            }

            return vocab;
        }

        public EncodedBatch EncodeBatch(string[] texts)
        {
            var batchSize = texts.Length;
            var inputIds = new int[batchSize][];
            var attentionMask = new int[batchSize][];
            var tokenTypeIds = new int[batchSize][];
            int maxLength = 0;

            // Первый проход: токенизация и определение максимальной длины
            for (int b = 0; b < batchSize; b++)
            {
                var tokens = Tokenize(texts[b]);
                var ids = new List<int> { _clsTokenId }; // [CLS] в начале

                foreach (var token in tokens)
                {
                    if (ids.Count >= _maxSequenceLength - 1) break;
                    ids.Add(_vocab.TryGetValue(token, out var id) ? id : _unkTokenId);
                }

                if (ids.Count < _maxSequenceLength)
                    ids.Add(_sepTokenId); // [SEP] в конце

                inputIds[b] = ids.ToArray();
                attentionMask[b] = new int[ids.Count];
                Array.Fill(attentionMask[b], 1);
                tokenTypeIds[b] = new int[ids.Count]; // Все 0 для SBERT

                maxLength = Math.Max(maxLength, ids.Count);
            }

            // Второй проход: паддинг до maxLength
            for (int b = 0; b < batchSize; b++)
            {
                if (inputIds[b].Length < maxLength)
                {
                    Array.Resize(ref inputIds[b], maxLength);
                    Array.Resize(ref attentionMask[b], maxLength);
                    Array.Resize(ref tokenTypeIds[b], maxLength);

                    // заполнение паддингом
                    for (int i = inputIds[b].Length - (maxLength - inputIds[b].Where(x => x != 0).Count()); i < maxLength; i++)
                    {
                        if (inputIds[b][i] == 0)
                        {
                            inputIds[b][i] = _padTokenId;
                            attentionMask[b][i] = 0;
                        }
                    }
                }
            }

            return new EncodedBatch(inputIds, attentionMask, tokenTypeIds, maxLength);
        }

        private string[] Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            // Базовая токенизация: по пробелам и знакам препинания
            return Regex.Split(text.ToLowerInvariant(), @"[\s\p{P}]+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Take(_maxSequenceLength - 2)
                .ToArray();
        }

        public void Dispose() { }
    }

    public class EncodedBatch
    {
        public int[][] InputIds { get; }         
        public int[][] AttentionMask { get; }    
        public int[][] TokenTypeIds { get; }      
        public int MaxLength { get; }

        public EncodedBatch(int[][] inputIds, int[][] attentionMask, int[][] tokenTypeIds, int maxLength)
        {
            InputIds = inputIds;
            AttentionMask = attentionMask;
            TokenTypeIds = tokenTypeIds;
            MaxLength = maxLength;
        }
    }
}