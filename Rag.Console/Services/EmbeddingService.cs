using System.Text.RegularExpressions;
using OpenAI.Embeddings;

namespace Rag.Console.Services;

public class EmbeddingService
{
    private const int EmbeddingDimensions = 1536;

    private readonly EmbeddingClient? _client;

    public EmbeddingService(string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new EmbeddingClient(
                model: "text-embedding-3-small",
                apiKey: apiKey);
        }
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text)
    {
        var sanitizedText = TextSanitizer.Sanitize(text);

        if (_client is null)
        {
            return GenerateLocalEmbedding(sanitizedText);
        }

        try
        {
            var result = await _client.GenerateEmbeddingAsync(sanitizedText);

            return result.Value.ToFloats();
        }
        catch
        {
            return GenerateLocalEmbedding(sanitizedText);
        }
    }

    private static ReadOnlyMemory<float> GenerateLocalEmbedding(string text)
    {
        var vector = new float[EmbeddingDimensions];

        foreach (var token in Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+"))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var hash = GetStableHash(token);
            var index = (int)(hash % EmbeddingDimensions);
            vector[index] += 1f;
        }

        var magnitude = MathF.Sqrt(vector.Sum(value => value * value));

        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }

    private static uint GetStableHash(string value)
    {
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;

            var hash = fnvOffset;

            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= fnvPrime;
            }

            return hash;
        }
    }
}