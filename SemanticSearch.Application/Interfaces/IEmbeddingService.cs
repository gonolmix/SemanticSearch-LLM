using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface IEmbeddingService
    {
        /// <summary>
        /// Инициализация модели (загрузка в память)
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Генерация эмбеддинга для одного текста
        /// </summary>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Генерация эмбеддингов для множества текстов (пакетно)
        /// </summary>
        Task<float[][]> GenerateEmbeddingsAsync(string[] texts);

        /// <summary>
        /// Размерность вектора модели
        /// </summary>
        int VectorDimension { get; }

        /// <summary>
        /// Название модели
        /// </summary>
        string ModelName { get; }

        /// <summary>
        /// Готова ли модель к работе
        /// </summary>
        bool IsReady { get; }
    }
}
