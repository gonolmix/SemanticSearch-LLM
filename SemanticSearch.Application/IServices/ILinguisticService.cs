using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.IServices
{
    public interface ILinguisticService
    {
        Task LoadDataAsync(); // Загрузка стоп-слов и синонимов в память
        string NormalizeWord(string word); // Приведение к нижнему регистру + стемминг
        List<string> ExpandQuery(List<string> tokens); // Добавление синонимов
        bool IsStopWord(string word);
        List<string> Tokenize(string text); // Разбиение текста на слова
    }
}
