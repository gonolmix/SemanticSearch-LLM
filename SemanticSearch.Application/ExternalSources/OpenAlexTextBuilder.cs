namespace SemanticSearch.Application.ExternalSources
{
    public static class OpenAlexTextBuilder
    {
        /// <summary>
        /// Собирает текст аннотации из инвертированного индекса OpenAlex.
        /// Индекс имеет формат: {"word1": [0, 5, 12], "word2": [1, 6, 13], ...}
        /// </summary>
        public static string? Reconstruct(Dictionary<string, int[]>? invertedIndex)
        {
            if (invertedIndex is null || invertedIndex.Count == 0)
                return null;

            // Находим максимальную позицию, чтобы определить длину массива
            var maxLength = invertedIndex.Values
                .Where(positions => positions.Length > 0)
                .Max(positions => positions[^1] + 1);

            if (maxLength == 0)
                return null;

            // Создаём массив слотов для слов
            var slots = new string?[maxLength];

            // Заполняем слоты словами согласно их позициям
            foreach (var (word, positions) in invertedIndex)
            {
                foreach (var position in positions)
                {
                    if (position >= 0 && position < maxLength)
                    {
                        slots[position] = word;
                    }
                }
            }

            // Собираем результат, пропуская пустые слоты
            var result = string.Join(" ", slots.Where(slot => slot is not null));

            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}