using Rag.Console.Models;

namespace Rag.Console.Services;

public class TextChunker
{
    public List<DocumentChunk> Chunk(
        DocumentContent document,
        int chunkSize = 100,
        int overlap = 10)
    {
        var chunks = new List<DocumentChunk>();

        var text = document.Content;

        var start = 0;
        var chunkNumber = 1;

        while (start < text.Length)
        {
            var length = Math.Min(
                chunkSize,
                text.Length - start);

            var chunkText = text.Substring(start, length);

            chunks.Add(new DocumentChunk
            {
                FileName = document.FileName,
                ChunkNumber = chunkNumber,
                Content = chunkText
            });

            start += chunkSize - overlap;
            chunkNumber++;
        }

        return chunks;
    }
}