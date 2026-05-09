using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Helpers
{
    public static class VectorMath
    {
        /// <summary>
        /// Косинусное сходство между двумя векторами
        /// </summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same dimension");

            float dotProduct = 0;
            float normA = 0;
            float normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0)
                return 0;

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        /// <summary>
        /// Нормализация вектора (L2 норма)
        /// </summary>
        public static float[] Normalize(float[] vector)
        {
            float norm = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                norm += vector[i] * vector[i];
            }

            norm = MathF.Sqrt(norm);
            if (norm == 0)
                return vector;

            var normalized = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = vector[i] / norm;
            }

            return normalized;
        }

        /// <summary>
        /// Конвертация byte[] в float[] (безопасная версия)
        /// </summary>
        public static float[] BytesToFloats(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<float>();

            if (bytes.Length % 4 != 0)
                throw new ArgumentException($"Byte array length must be multiple of 4, got {bytes.Length}");

            var floats = new float[bytes.Length / 4];

            // Безопасная конвертация через BitConverter
            for (int i = 0; i < floats.Length; i++)
            {
                floats[i] = BitConverter.ToSingle(bytes, i * 4);
            }

            return floats;
        }

        /// <summary>
        /// Конвертация float[] в byte[] (согласованная версия)
        /// </summary>
        public static byte[] FloatsToBytes(float[] floats)
        {
            if (floats == null || floats.Length == 0)
                return Array.Empty<byte>();

            var bytes = new byte[floats.Length * 4];

            for (int i = 0; i < floats.Length; i++)
            {
                Buffer.BlockCopy(floats, i * 4, bytes, i * 4, 4);
            }

            return bytes;
        }

        /// <summary>
        /// Средний вектор из множества векторов
        /// </summary>
        public static float[] Average(float[][] vectors)
        {
            if (vectors.Length == 0)
                throw new ArgumentException("At least one vector required");

            var dimension = vectors[0].Length;
            var average = new float[dimension];

            for (int i = 0; i < vectors.Length; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    average[j] += vectors[i][j];
                }
            }

            for (int j = 0; j < dimension; j++)
            {
                average[j] /= vectors.Length;
            }

            return Normalize(average);
        }
    }
}
