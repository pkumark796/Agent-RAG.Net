using OpenAI;
using OpenAI.Chat;
using Rag.Console.Models;
using System.Text;
using System.ClientModel;

namespace Rag.Console.Services;

public class RagService
{
    private readonly ChatClient _chatClient;

    public RagService(string apiKey)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.groq.com/openai/v1")
        };

        _chatClient = new ChatClient(
            model: "openai/gpt-oss-120b",
            credential: new ApiKeyCredential(apiKey),
            options: options);
    }

    public async Task<string> GenerateAnswerAsync(
        string question,
        List<(DocumentChunk Chunk, double Score)> results)
    {
        var contextBuilder = new StringBuilder();

        foreach (var result in results)
        {
            contextBuilder.AppendLine(
                $"SOURCE: {result.Chunk.FileName}");

            contextBuilder.AppendLine(
                $"CHUNK: {result.Chunk.ChunkNumber}");

            contextBuilder.AppendLine();

            contextBuilder.AppendLine(
                result.Chunk.Content);

            contextBuilder.AppendLine();

            contextBuilder.AppendLine(
                "-----------------------------");
        }

        var context = contextBuilder.ToString();

        var prompt = $"""
            Answer the user's question using ONLY the
            information contained in the provided context.

            If the answer cannot be found in the context,
            say:

            "I could not find that information in the provided documents."

            Do not use outside knowledge.

            At the end of your answer, include the source
            document name.

            CONTEXT:

            {context}

            QUESTION:

            {question}
            """;

        ChatCompletion completion =
            await _chatClient.CompleteChatAsync(prompt);

        return completion.Content[0].Text;
    }
}