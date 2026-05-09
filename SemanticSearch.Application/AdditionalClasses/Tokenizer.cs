using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.AdditionalClasses
{
    public class Tokenizer : IDisposable
    {
        private readonly Dictionary<string, int> _vocab;
        private readonly int _maxSequenceLength = 128;
        private readonly int _padTokenId = 0;
        private readonly int _clsTokenId = 101;
        private readonly int _sepTokenId = 102;

        public Tokenizer(string modelPath)
        {
            var vocabPath = Path.Combine(modelPath, "vocab.txt");
            _vocab = LoadVocab(vocabPath);
        }

        private Dictionary<string, int> LoadVocab(string path)
        {
            var vocab = new Dictionary<string, int>();
            if (!File.Exists(path))
                return vocab;

            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                vocab[lines[i]] = i;
            }
            return vocab;
        }

        public EncodedBatch EncodeBatch(string[] texts)
        {
            var batchSize = texts.Length;
            var inputIds = new DenseTensor<int>(new[] { batchSize, _maxSequenceLength });
            var attentionMask = new DenseTensor<int>(new[] { batchSize, _maxSequenceLength });
            var tokenTypeIds = new DenseTensor<int>(new[] { batchSize, _maxSequenceLength });

            for (int b = 0; b < batchSize; b++)
            {
                var tokens = Tokenize(texts[b]);

                // Добавляем [CLS] в начало
                inputIds[b, 0] = _clsTokenId;
                attentionMask[b, 0] = 1;

                // Заполняем токенами
                for (int i = 0; i < Math.Min(tokens.Length, _maxSequenceLength - 2); i++)
                {
                    inputIds[b, i + 1] = _vocab.TryGetValue(tokens[i], out var id) ? id : _vocab.TryGetValue("[UNK]", out var unk) ? unk : 0;
                    attentionMask[b, i + 1] = 1;
                }

                // Добавляем [SEP] в конец
                int lastIdx = Math.Min(tokens.Length + 1, _maxSequenceLength - 1);
                inputIds[b, lastIdx] = _sepTokenId;
                attentionMask[b, lastIdx] = 1;

                // token_type_ids всегда 0 для SBERT
                for (int i = 0; i < _maxSequenceLength; i++)
                {
                    tokenTypeIds[b, i] = 0;
                }
            }

            return new EncodedBatch(inputIds, attentionMask, tokenTypeIds);
        }

        private string[] Tokenize(string text)
        {
            // Простая токенизация по пробелам и знакам препинания
            // В продакшене используйте WordPiece токенизатор
            return text
                .ToLower()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']' },
                       StringSplitOptions.RemoveEmptyEntries);
        }

        public void Dispose() { }
    }
}
