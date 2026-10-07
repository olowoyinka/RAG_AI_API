using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RAG_AI_API.DTOs;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RAG_AI_API.Services;


public interface ITextExtractionService
{
    bool CanHandle(string contentType, string fileName);

    Task<ExtractedDocument> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
}


public class TextExtractionService : ITextExtractionService
{
    private static readonly string[] Extensions = [".pdf", ".docx", ".txt", ".md"];


    public bool CanHandle(string contentType, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return Extensions.Contains(extension) || contentType is "application/pdf"
                                               or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                                               or "text/plain"
                                               or "text/markdown";
    }


    public async Task<ExtractedDocument> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => await ExtractPdfAsync(stream, fileName, cancellationToken),
            ".docx" => await ExtractDocxAsync(stream, fileName, cancellationToken),
            ".txt" or ".md" => await ExtractPlainTextAsync(stream, fileName, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported document type: {contentType}")
        };
    }


    private static Task<ExtractedDocument> ExtractPdfAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);

        var pages = document.GetPages()
                            .Select(page => new ExtractedPage(page.Number, ContentOrderTextExtractor.GetText(page)))
                            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                            .ToList();

        return Task.FromResult(new ExtractedDocument(fileName, pages));
    }


    private static async Task<ExtractedDocument> ExtractDocxAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var document = WordprocessingDocument.Open(memory, false);
        var body = document.MainDocumentPart?.Document?.Body ?? throw new InvalidOperationException("DOCX does not contain a document body.");

        var paragraphs = body.Descendants<Paragraph>()
                            .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

        return new ExtractedDocument(fileName, [new ExtractedPage(1, string.Join(Environment.NewLine, paragraphs))]);
    }


    private static async Task<ExtractedDocument> ExtractPlainTextAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return new ExtractedDocument(fileName, [new ExtractedPage(1, text)]);
    }
}
