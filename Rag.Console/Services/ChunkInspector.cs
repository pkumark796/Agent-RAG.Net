using Rag.Console.Models;

namespace Rag.Console.Services;

// Debug/inspection tool for viewing indexed chunks and their metadata.
public class ChunkInspector
{
    private readonly VectorStore _vectorStore;

    public ChunkInspector(VectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task ListChunksAsync(string? fileNameFilter = null)
    {
        var chunks = (await _vectorStore.GetAllAsync())
            .Where(c => fileNameFilter == null ||
                        c.FileName.Contains(fileNameFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.FileName)
            .ThenBy(c => c.ChunkNumber)
            .ToList();

        if (chunks.Count == 0)
        {
            System.Console.WriteLine("No chunks found.");
            return;
        }

        System.Console.WriteLine();
        System.Console.WriteLine("==========================================");
        System.Console.WriteLine($" CHUNKS ({chunks.Count})");
        System.Console.WriteLine("==========================================");

        foreach (var chunk in chunks)
        {
            var preview = Preview(chunk.Content);

            System.Console.WriteLine();
            System.Console.WriteLine($"Id         : {chunk.Id}");
            System.Console.WriteLine($"File       : {chunk.FileName}");
            System.Console.WriteLine($"Chunk #    : {chunk.ChunkNumber}");
            System.Console.WriteLine($"Characters : {chunk.Content.Length}");
            System.Console.WriteLine($"Embedding  : {chunk.Embedding.Length} dimensions");
            System.Console.WriteLine($"Preview    : {preview}");
        }

        System.Console.WriteLine();
    }

    public async Task ShowChunkAsync(string fileName, int chunkNumber)
    {
        var chunk = (await _vectorStore.GetAllAsync())
            .FirstOrDefault(c =>
                c.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
                c.ChunkNumber == chunkNumber);

        if (chunk == null)
        {
            System.Console.WriteLine(
                $"No chunk found for '{fileName}' #{chunkNumber}.");
            return;
        }

        System.Console.WriteLine();
        System.Console.WriteLine("==========================================");
        System.Console.WriteLine($" CHUNK DETAIL");
        System.Console.WriteLine("==========================================");
        System.Console.WriteLine($"Id         : {chunk.Id}");
        System.Console.WriteLine($"File       : {chunk.FileName}");
        System.Console.WriteLine($"Chunk #    : {chunk.ChunkNumber}");
        System.Console.WriteLine($"Characters : {chunk.Content.Length}");
        System.Console.WriteLine($"Embedding  : {chunk.Embedding.Length} dimensions");
        System.Console.WriteLine();
        System.Console.WriteLine("Content:");
        System.Console.WriteLine(chunk.Content);
        System.Console.WriteLine();
    }

    private static string Preview(string content)
    {
        var singleLine = content.Replace('\n', ' ').Replace('\r', ' ').Trim();

        return singleLine.Length <= 80
            ? singleLine
            : singleLine[..80] + "...";
    }
}
