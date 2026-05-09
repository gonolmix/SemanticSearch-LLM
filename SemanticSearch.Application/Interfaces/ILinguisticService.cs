using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface ILinguisticService
    {
        /// <summary>
        /// Загрузка стоп-слов и локальных синонимов из БД
        /// </summary>
        Task LoadDataAsync();

        /// <summary>
        /// Токенизация текста с фильтрацией стоп-слов
        /// </summary>
        List<string> Tokenize(string text);

        /// <summary>
        /// Проверка: является ли слово стоп-словом
        /// </summary>
        bool IsStopWord(string word);

        /// <summary>
        /// Нормализация слова (лемматизация + lower)
        /// </summary>
        string NormalizeWord(string word);

        /// <summary>
        /// Расширение запроса синонимами (БД + API)
        /// </summary>
        Task<List<string>> ExpandQueryAsync(List<string> tokens);

        /// <summary>
        /// Получить только значимые токены (длина > 3, не стоп-слова)
        /// </summary>
        List<string> GetSignificantTokens(List<string> tokens);

        /// <summary>
        /// Очистить кэш лемматизации (при обновлении правил)
        /// </summary>
        void ClearCache();
    }
}
