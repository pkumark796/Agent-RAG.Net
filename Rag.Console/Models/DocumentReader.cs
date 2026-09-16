using DocumentFormat.OpenXml.Packaging;
using Rag.Console.Models;
using System.Text;
using UglyToad.PdfPig;

namespace Rag.Console.Services;

public class DocumentReader
{
    public async Task<DocumentContent> ReadAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var content = extension switch
        {
            ".txt" => await File.ReadAllTextAsync(filePath),
            ".md" => await File.ReadAllTextAsync(filePath),
            ".pdf" => ReadPdf(filePath),
            ".docx" => ReadWord(filePath),

            _ => throw new NotSupportedException(
                $"File type {extension} is not supported.")
        };

        return new DocumentContent
        {
            FileName = Path.GetFileName(filePath),
            Content = content
        };
    }

    private static string ReadPdf(string filePath)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ReadWord(string filePath)
    {
        using var document =
            WordprocessingDocument.Open(filePath, false);

        return document.MainDocumentPart?
                   .Document
                   .Body?
                   .InnerText
               ?? string.Empty;
    }
}