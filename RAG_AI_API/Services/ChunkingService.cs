using Microsoft.Extensions.Options;
using RAG_AI_API.DTOs;
using System.Text.RegularExpressions;

namespace RAG_AI_API.Services;


public interface IChunkingService
{
    IReadOnlyList<TextChunk> Chunk(ExtractedDocument document);
}


public class ChunkingService : IChunkingService
{
    private readonly RagOptions _ragOptions;

    public ChunkingService(IOptions<RagOptions> options)
    {
        _ragOptions = options.Value;
    }


    public IReadOnlyList<TextChunk> Chunk(ExtractedDocument document)
    {
        var result = new List<TextChunk>();
        var index = 0;

        foreach (var page in document.Pages)
        {
            var paragraphs = page.Text.Replace("\r\n", "\n").Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var current = new List<string>();
            var currentTokens = 0;

            foreach (var paragraph in paragraphs)
            {
                var tokens = EstimateTokens(paragraph);

                if (current.Count > 0 && currentTokens + tokens > _ragOptions.ChunkMaximumTokens)
                {
                    result.Add(CreateChunk(index++, current, page.PageNumber));

                    current = TakeOverlap(current);
                    
                    currentTokens = EstimateTokens(string.Join(" ", current));
                }

                current.Add(paragraph);

                currentTokens += tokens;

                if (currentTokens >= _ragOptions.ChunkTargetTokens)
                {
                    result.Add(CreateChunk(index++, current, page.PageNumber));

                    current = TakeOverlap(current);
                    
                    currentTokens = EstimateTokens(string.Join(" ", current));
                }
            }

            if (current.Count > 0)
                result.Add(CreateChunk(index++, current, page.PageNumber));
        }

        return result;
    }


    private TextChunk CreateChunk(int index, IReadOnlyList<string> parts, int page)
    {
        var content = string.Join("\n\n", parts).Trim();

        return new TextChunk(index, content, DetectHeading(parts), page, EstimateTokens(content));
    }


    private List<string> TakeOverlap(List<string> paragraphs)
    {
        if (_ragOptions.ChunkOverlapTokens <= 0) 
            return [];

        var output = new List<string>();
        var tokens = 0;

        for (var i = paragraphs.Count - 1; i >= 0; i--)
        {
            var count = EstimateTokens(paragraphs[i]);

            if (tokens + count > _ragOptions.ChunkOverlapTokens) 
                break;

            output.Insert(0, paragraphs[i]);

            tokens += count;
        }

        return output;
    }


    private static string? DetectHeading(IReadOnlyList<string> parts)
    {
        if (parts.Count == 0) return null;

        var first = parts[0].Trim();

        return first.Length <= 120 &&
               !first.EndsWith('.') &&
               !first.EndsWith(',') &&
               !first.EndsWith(';')
            ? first
            : null;
    }


    private static int EstimateTokens(string text) =>
        Math.Max(1, Regex.Matches(text, @"\S+").Count * 4 / 3);
}
