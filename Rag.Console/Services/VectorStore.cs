using Npgsql;
using Pgvector;
using Rag.Console.Models;

namespace Rag.Console.Services;

// Persists chunks + embeddings in PostgreSQL using the pgvector extension.
public class VectorStore
{
    private readonly NpgsqlDataSource _dataSource;

    public VectorStore(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();

        _dataSource = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using (var extensionCommand =
            new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", connection))
        {
            await extensionCommand.ExecuteNonQueryAsync();
        }

        await using var tableCommand = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS document_chunks (
                id uuid PRIMARY KEY,
                file_name text NOT NULL,
                chunk_number int NOT NULL,
                content text NOT NULL,
                embedding vector(1536) NOT NULL
            );
            """,
            connection);

        await tableCommand.ExecuteNonQueryAsync();

        await using var clearCommand = new NpgsqlCommand(
            "TRUNCATE TABLE document_chunks;",
            connection);

        await clearCommand.ExecuteNonQueryAsync();
    }

    public async Task AddAsync(DocumentChunk chunk)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO document_chunks (id, file_name, chunk_number, content, embedding)
            VALUES (@id, @file_name, @chunk_number, @content, @embedding)
            ON CONFLICT (id) DO UPDATE SET
                file_name = EXCLUDED.file_name,
                chunk_number = EXCLUDED.chunk_number,
                content = EXCLUDED.content,
                embedding = EXCLUDED.embedding;
            """,
            connection);

        command.Parameters.AddWithValue("id", Guid.Parse(chunk.Id));
        command.Parameters.AddWithValue("file_name", chunk.FileName);
        command.Parameters.AddWithValue("chunk_number", chunk.ChunkNumber);
        command.Parameters.AddWithValue("content", chunk.Content);
        command.Parameters.AddWithValue("embedding", new Vector(chunk.Embedding));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<(DocumentChunk Chunk, double Score)>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 3)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT id, file_name, chunk_number, content, embedding,
                   1 - (embedding <=> @query_embedding) AS similarity
            FROM document_chunks
            ORDER BY embedding <=> @query_embedding
            LIMIT @top_k;
            """,
            connection);

        command.Parameters.AddWithValue("query_embedding", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("top_k", topK);

        var results = new List<(DocumentChunk Chunk, double Score)>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var chunk = ReadChunk(reader);
            var similarity = reader.GetDouble(5);

            results.Add((chunk, similarity));
        }

        return results;
    }

    public async Task<List<DocumentChunk>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id, file_name, chunk_number, content, embedding FROM document_chunks;",
            connection);

        var chunks = new List<DocumentChunk>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            chunks.Add(ReadChunk(reader));
        }

        return chunks;
    }

    private static DocumentChunk ReadChunk(NpgsqlDataReader reader)
    {
        var embedding = reader.GetFieldValue<Vector>(4);

        return new DocumentChunk
        {
            Id = reader.GetGuid(0).ToString(),
            FileName = reader.GetString(1),
            ChunkNumber = reader.GetInt32(2),
            Content = reader.GetString(3),
            Embedding = embedding.ToArray()
        };
    }
}
