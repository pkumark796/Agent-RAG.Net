using Microsoft.Extensions.Configuration;
using Rag.Console.Models;
using Rag.Console.Services;

// ============================================================
// 1. Get Configuration (user secrets + environment variables)
// ============================================================

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var groqApiKey = configuration["GROQ_API_KEY"];
var openAiApiKey = configuration["OPENAI_API_KEY"];

if (string.IsNullOrWhiteSpace(groqApiKey))
{
    Console.WriteLine("ERROR: GROQ_API_KEY is not configured.");
    Console.WriteLine();
    Console.WriteLine("Set it using:");
    Console.WriteLine("dotnet user-secrets set \"GROQ_API_KEY\" \"your-api-key\"");
    return;
}

var dbConnectionString = configuration["RAG_DB_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(dbConnectionString))
{
    Console.WriteLine("ERROR: RAG_DB_CONNECTION_STRING is not configured.");
    Console.WriteLine();
    Console.WriteLine("Set it using:");
    Console.WriteLine("dotnet user-secrets set \"RAG_DB_CONNECTION_STRING\" \"Host=...;Database=...;Username=...;Password=...\"");
    return;
}


// ============================================================
// 2. Create Services
// ============================================================

var reader = new DocumentReader();
var chunker = new TextChunker();
var embeddingService = new EmbeddingService(openAiApiKey);
var vectorStore = new VectorStore(dbConnectionString);
var ragService = new RagService(groqApiKey);
var chunkInspector = new ChunkInspector(vectorStore);

await vectorStore.InitializeAsync();


// ============================================================
// 3. Locate Documents Folder
// ============================================================

var documentsFolder = Path.Combine(
    Directory.GetCurrentDirectory(),
    "Documents");

if (!Directory.Exists(documentsFolder))
{
    Console.WriteLine(
        $"Documents folder not found: {documentsFolder}");

    return;
}


// ============================================================
// 4. Find Documents
// ============================================================

var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".txt",
    ".md",
    ".pdf",
    ".docx"
};

var files = Directory
    .EnumerateFiles(documentsFolder, "*.*", SearchOption.AllDirectories)
    .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine(
        $"No documents found in: {documentsFolder}");

    return;
}

Console.WriteLine();
Console.WriteLine("==========================================");
Console.WriteLine("        .NET RAG WORKSHOP");
Console.WriteLine("==========================================");
Console.WriteLine();

Console.WriteLine(
    $"Documents folder: {documentsFolder}");

Console.WriteLine(
    $"Documents found: {files.Length}");

Console.WriteLine();


// ============================================================
// 5. Read, Chunk, Embed and Index Documents
// ============================================================

var allChunks = new List<DocumentChunk>();

Console.WriteLine("==========================================");
Console.WriteLine(" DOCUMENT INGESTION");
Console.WriteLine("==========================================");
Console.WriteLine();

foreach (var file in files)
{
    try
    {
        Console.WriteLine(
            $"Reading: {Path.GetFileName(file)}");

        // ----------------------------------------------------
        // Read document
        // ----------------------------------------------------

        var document =
            await reader.ReadAsync(file);


        // ----------------------------------------------------
        // Chunk document
        // ----------------------------------------------------

        var chunks =
            chunker.Chunk(document);

        Console.WriteLine(
            $"Created {chunks.Count} chunk(s)");


        // ----------------------------------------------------
        // Generate embedding for every chunk
        // ----------------------------------------------------

        foreach (var chunk in chunks)
        {
            Console.WriteLine(
                $"Generating embedding: " +
                $"{chunk.FileName} " +
                $"Chunk {chunk.ChunkNumber}");

            chunk.Embedding =
                await embeddingService.GenerateEmbeddingAsync(
                    chunk.Content);


            // ------------------------------------------------
            // Store in vector database
            // ------------------------------------------------

            await vectorStore.AddAsync(chunk);

            allChunks.Add(chunk);
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine();

        Console.WriteLine(
            $"ERROR processing {Path.GetFileName(file)}");

        Console.WriteLine(
            ex.Message);

        Console.WriteLine();
    }
}


// ============================================================
// 6. Verify Documents Were Indexed
// ============================================================

Console.WriteLine("==========================================");

Console.WriteLine(
    $"Total chunks indexed: {allChunks.Count}");

Console.WriteLine("==========================================");
Console.WriteLine();

if (allChunks.Count == 0)
{
    Console.WriteLine(
        "No document chunks were successfully indexed.");

    return;
}


// ============================================================
// 7. Start RAG Question/Answer Loop
// ============================================================

Console.WriteLine("RAG is ready.");
Console.WriteLine();

Console.WriteLine(
    "Ask questions about your documents.");

Console.WriteLine(
    "Type 'exit' to quit.");

Console.WriteLine(
    "Type 'chunks' to list indexed chunks, " +
    "'chunks <filename>' to filter, " +
    "or 'chunk <filename> <number>' to view one chunk in full.");


// ============================================================
// 8. Question Loop
// ============================================================

while (true)
{
    Console.WriteLine();
    Console.WriteLine("------------------------------------------");

    Console.Write("Question: ");

    var question = Console.ReadLine();


    // --------------------------------------------------------
    // Ignore empty input
    // --------------------------------------------------------

    if (string.IsNullOrWhiteSpace(question))
    {
        continue;
    }


    // --------------------------------------------------------
    // Exit
    // --------------------------------------------------------

    if (question.Equals(
        "exit",
        StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine();
        Console.WriteLine("Goodbye.");

        break;
    }


    // --------------------------------------------------------
    // Chunk/metadata inspection commands
    // --------------------------------------------------------

    var commandParts = question.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (commandParts[0].Equals("chunks", StringComparison.OrdinalIgnoreCase))
    {
        var fileNameFilter = commandParts.Length > 1
            ? commandParts[1]
            : null;

        await chunkInspector.ListChunksAsync(fileNameFilter);
        continue;
    }

    if (commandParts[0].Equals("chunk", StringComparison.OrdinalIgnoreCase))
    {
        if (commandParts.Length != 3 ||
            !int.TryParse(commandParts[2], out var chunkNumber))
        {
            Console.WriteLine(
                "Usage: chunk <filename> <number>");

            continue;
        }

        await chunkInspector.ShowChunkAsync(commandParts[1], chunkNumber);
        continue;
    }


    try
    {
        // ====================================================
        // RETRIEVAL
        // ====================================================

        Console.WriteLine();
        Console.WriteLine("Searching documents...");


        // ----------------------------------------------------
        // Convert question into embedding
        // ----------------------------------------------------

        var questionEmbedding =
            await embeddingService.GenerateEmbeddingAsync(
                question);


        // ----------------------------------------------------
        // Vector similarity search
        // ----------------------------------------------------

        var results =
            await vectorStore.SearchAsync(
                questionEmbedding,
                topK: 3);


        // ----------------------------------------------------
        // Display retrieved documents
        // ----------------------------------------------------

        Console.WriteLine();

        Console.WriteLine(
            "Retrieved document chunks:");

        Console.WriteLine();

        foreach (var result in results)
        {
            Console.WriteLine(
                $"Source     : {result.Chunk.FileName}");

            Console.WriteLine(
                $"Chunk      : {result.Chunk.ChunkNumber}");

            Console.WriteLine(
                $"Similarity : {result.Score:F4}");

            Console.WriteLine();
        }


        // ====================================================
        // GENERATION
        // ====================================================

        Console.WriteLine(
            "Generating answer using retrieved context...");

        Console.WriteLine();


        var answer =
            await ragService.GenerateAnswerAsync(
                question,
                results);


        // ====================================================
        // Display Final Answer
        // ====================================================

        Console.WriteLine(
            "==========================================");

        Console.WriteLine(
            "ANSWER");

        Console.WriteLine(
            "==========================================");

        Console.WriteLine();

        Console.WriteLine(answer);

        Console.WriteLine();

        Console.WriteLine(
            "==========================================");
    }
    catch (Exception ex)
    {
        Console.WriteLine();

        Console.WriteLine(
            "ERROR processing question:");

        Console.WriteLine(
            ex.Message);
    }
}