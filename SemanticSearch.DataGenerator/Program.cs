using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SemanticSearch.Core.Entities;
using SemanticSearch.Infrastructure.Data;
using Bogus;
using System.Text;

namespace SemanticSearch.DataGenerator
{
    // 🔥 Вспомогательные классы для данных (чтобы избежать ошибок кортежей)
    class AiTopic
    {
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string[] Snippets { get; set; } = Array.Empty<string>();
    }

    class RandomTopic
    {
        public string Name { get; set; } = string.Empty;
        public string[] Subtopics { get; set; } = Array.Empty<string>();
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Neural Network Document Generator");
            Console.WriteLine("===================================\n");

            // Загрузка конфигурации
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var connectionString = config["ConnectionStrings:LocalConnection"];
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Connection string not found in appsettings.json");
                Console.WriteLine("Please copy appsettings.json from SemanticSearch.Web project or create it.");
                return;
            }

            // Парсинг аргументов
            int relevantDocs = 200;
            int randomDocs = 800;
            int paragraphsPerDoc = 2;

            if (args.Length >= 1 && int.TryParse(args[0], out int r)) relevantDocs = r;
            if (args.Length >= 2 && int.TryParse(args[1], out int n)) randomDocs = n;
            if (args.Length >= 3 && int.TryParse(args[2], out int p)) paragraphsPerDoc = p;

            Console.WriteLine($"Configuration:");
            Console.WriteLine($"   - Relevant documents: {relevantDocs}");
            Console.WriteLine($"   - Random documents: {randomDocs}");
            Console.WriteLine($"   - Paragraphs per doc: {paragraphsPerDoc}");
            Console.WriteLine($"   - Total: {relevantDocs + randomDocs} documents\n");

            // Подключение к БД
            Console.WriteLine("Connecting to database...");
            var contextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var context = new AppDbContext(contextOptions);

            try
            {
                await context.Database.CanConnectAsync();
                Console.WriteLine("Connected to database successfully\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot connect to database: {ex.Message}");
                return;
            }

            // Генерация и вставка документов
            Console.WriteLine("Generating documents...");

            var relevantDocsList = GenerateRelevantDocuments(relevantDocs, paragraphsPerDoc);
            var randomDocsList = GenerateRandomDocuments(randomDocs, paragraphsPerDoc);

            var allDocs = relevantDocsList.Concat(randomDocsList).ToList();

            Console.WriteLine($"\nInserting {allDocs.Count} documents into database...");

            await InsertDocuments(context, allDocs);

            Console.WriteLine("\nGeneration completed successfully!");
            Console.WriteLine($"   - Total documents: {allDocs.Count}");
            Console.WriteLine($"   - Relevant (AI/ML): {relevantDocs}");
            Console.WriteLine($"   - Random topics: {randomDocs}");
        }

        static List<DocumentWithParagraphs> GenerateRelevantDocuments(int count, int paragraphsPerDoc)
        {
            var faker = new Faker();
            var docs = new List<DocumentWithParagraphs>();

            // Используем классы AiTopic вместо кортежей
            var aiTopics = new List<AiTopic>
            {
                new AiTopic { Name = "Large Language Models", ShortName = "LLMs", Snippets = new[]
                {
                    "Scaling laws in language models show that performance improves predictably with compute, data, and parameters.",
                    "Instruction tuning transforms base LLMs into helpful assistants by fine-tuning on human demonstrations.",
                    "Chain-of-thought prompting enables LLMs to solve complex reasoning tasks by generating intermediate steps.",
                    "Retrieval-augmented generation (RAG) reduces hallucinations by grounding responses in external knowledge.",
                    "Mixture-of-Experts (MoE) architectures like Mixtral achieve high capacity with efficient inference."
                }},
                new AiTopic { Name = "Computer Vision", ShortName = "CV", Snippets = new[]
                {
                    "Vision Transformers (ViT) achieve state-of-the-art results by treating images as sequences of patches.",
                    "Self-supervised pretraining with MAE (Masked Autoencoders) learns rich visual representations without labels.",
                    "Object detection with YOLOv8 provides real-time performance with accurate bounding box predictions.",
                    "Segment Anything Model (SAM) enables zero-shot image segmentation with prompt-based interaction.",
                    "Diffusion models for image generation produce higher quality outputs than previous GAN-based approaches."
                }},
                new AiTopic { Name = "Reinforcement Learning", ShortName = "RL", Snippets = new[]
                {
                    "Proximal Policy Optimization (PPO) balances exploration and stability for reliable policy updates.",
                    "Deep Q-Networks (DQN) with experience replay and target networks stabilize value-based learning.",
                    "Model-based RL methods like DreamerV3 learn world models for sample-efficient planning.",
                    "Multi-agent RL requires coordination mechanisms for cooperative or competitive environments.",
                    "Inverse RL infers reward functions from expert demonstrations when rewards are unknown."
                }},
                new AiTopic { Name = "Graph Neural Networks", ShortName = "GNN", Snippets = new[]
                {
                    "Message passing frameworks generalize convolution to irregular graph-structured data.",
                    "Graph Attention Networks (GAT) learn adaptive weights for neighbor aggregation.",
                    "GraphSAGE enables inductive learning on large graphs by sampling fixed-size neighborhoods.",
                    "Graph Transformers extend self-attention to graph-structured data with positional encodings.",
                    "Heterogeneous GNNs handle multiple node and edge types with type-specific transformations."
                }}
            };

            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var topic = aiTopics[random.Next(aiTopics.Count)];
                var paragraphs = GenerateParagraphs(topic.Snippets, paragraphsPerDoc, faker);

                docs.Add(new DocumentWithParagraphs
                {
                    Title = $"{topic.Name}: {GetRandomTitleSuffix(topic.Name)}",
                    Description = $"Comprehensive guide covering {topic.Name} - theory, methods, and applications.",
                    SourceType = "generated",
                    Paragraphs = paragraphs
                });
            }

            return docs;
        }

        static List<DocumentWithParagraphs> GenerateRandomDocuments(int count, int paragraphsPerDoc)
        {
            var faker = new Faker();
            var docs = new List<DocumentWithParagraphs>();

            var randomTopics = new List<RandomTopic>
            {
                new RandomTopic { Name = "Cooking", Subtopics = new[] { "recipes", "techniques", "cuisines", "ingredients", "meal prep" } },
                new RandomTopic { Name = "Travel", Subtopics = new[] { "destinations", "itineraries", "budget tips", "cultural experiences", "packing guides" } },
                new RandomTopic { Name = "Fitness", Subtopics = new[] { "workouts", "nutrition", "recovery", "training programs", "injury prevention" } },
                new RandomTopic { Name = "Finance", Subtopics = new[] { "investing", "budgeting", "retirement planning", "tax strategies", "debt management" } },
                new RandomTopic { Name = "Gardening", Subtopics = new[] { "plant care", "soil health", "seasonal planting", "pest control", "landscape design" } }
            };

            var contentTemplates = new[]
            {
                "This comprehensive guide covers {topic} in depth. Whether you're a beginner or experienced practitioner, you'll find actionable insights and proven techniques. Key areas include fundamentals, advanced methods, common pitfalls, and best practices for real-world application.",
                "Learn everything you need to know about {topic}. This resource provides step-by-step instructions, expert tips, and practical examples. Topics range from basic concepts to advanced strategies for maximizing results.",
                "Master {topic} with this detailed overview. We explore essential principles, proven methodologies, and emerging trends. Includes case studies, checklists, and resource recommendations for continued learning."
            };

            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var topic = randomTopics[random.Next(randomTopics.Count)];
                var subtopic = topic.Subtopics[random.Next(topic.Subtopics.Length)];
                var template = contentTemplates[random.Next(contentTemplates.Length)];
                var content = template.Replace("{topic}", $"{topic.Name} - {subtopic}");

                var paragraphs = new List<string> { content };
                for (int j = 1; j < paragraphsPerDoc; j++)
                {
                    paragraphs.Add(faker.Lorem.Paragraph(random.Next(3, 6)));
                }

                docs.Add(new DocumentWithParagraphs
                {
                    Title = $"{faker.Company.CatchPhrase()} - {topic.Name}: {subtopic}",
                    Description = $"Guide to {topic.Name} - {subtopic}",
                    SourceType = "generated",
                    Paragraphs = paragraphs
                });
            }

            return docs;
        }

        static List<string> GenerateParagraphs(string[] sourceSnippets, int count, Faker faker)
        {
            var random = new Random();
            var paragraphs = new List<string>();

            for (int i = 0; i < count; i++)
            {
                if (i < sourceSnippets.Length)
                {
                    paragraphs.Add(sourceSnippets[i]);
                }
                else
                {
                    paragraphs.Add(faker.Lorem.Paragraph(random.Next(3, 6)));
                }
            }

            return paragraphs;
        }

        static string GetRandomTitleSuffix(string topic)
        {
            var suffixes = new[]
            {
                $"A Complete Guide to {topic}",
                $"{topic}: Theory and Practice",
                $"Advanced {topic} Techniques",
                $"Essential {topic} Handbook",
                $"Mastering {topic}: Best Practices",
                $"The Ultimate {topic} Resource",
                $"{topic}: Current Trends and Future Directions"
            };

            return suffixes[new Random().Next(suffixes.Length)];
        }

        static async Task InsertDocuments(AppDbContext context, List<DocumentWithParagraphs> docs)
        {
            var batchSize = 100;
            var totalInserted = 0;

            for (int i = 0; i < docs.Count; i += batchSize)
            {
                var batch = docs.Skip(i).Take(batchSize).ToList();

                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var docData in batch)
                    {
                        var document = new Document
                        {
                            Title = docData.Title,
                            Description = docData.Description,
                            SourceType = docData.SourceType,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        await context.Documents.AddAsync(document);
                        await context.SaveChangesAsync();

                        for (int j = 0; j < docData.Paragraphs.Count; j++)
                        {
                            var paragraph = new Paragraph
                            {
                                DocumentId = document.Id,
                                Content = docData.Paragraphs[j],
                                ParagraphOrder = j + 1,
                                CreatedAt = DateTime.UtcNow
                            };

                            await context.Paragraphs.AddAsync(paragraph);
                        }

                        await context.SaveChangesAsync();
                        totalInserted++;

                        if (totalInserted % 50 == 0)
                        {
                            Console.WriteLine($"   Inserted {totalInserted}/{docs.Count} documents...");
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error at batch {i}: {ex.Message}");
                    throw;
                }
            }

            Console.WriteLine($"\nSuccessfully inserted {totalInserted} documents with paragraphs.");
        }
    }

    class DocumentWithParagraphs
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceType { get; set; } = "generated";
        public List<string> Paragraphs { get; set; } = new();
    }
}