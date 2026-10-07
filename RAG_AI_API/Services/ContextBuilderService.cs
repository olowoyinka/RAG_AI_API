using RAG_AI_API.DTOs;

namespace RAG_AI_API.Services;


public interface IContextBuilderService
{
    string Build(string question, IReadOnlyList<RankedChunk> chunks);
}


public class ContextBuilderService : IContextBuilderService
{
    public string Build(string question, IReadOnlyList<RankedChunk> chunks)
    {
        var builder = new System.Text.StringBuilder();

        foreach (var (chunk, index) in chunks.Select((x, i) => (x, i + 1)))
        {
            builder.AppendLine($"[SOURCE {index}]");
            builder.AppendLine($"Document: {chunk.DocumentName}");
            builder.AppendLine($"Page: {chunk.PageNumber}");

            if (!string.IsNullOrWhiteSpace(chunk.Heading))
                builder.AppendLine($"Heading: {chunk.Heading}");

            builder.AppendLine("Content:");
            builder.AppendLine(chunk.Content);
            builder.AppendLine();
            builder.AppendLine($"[/SOURCE {index}]");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
