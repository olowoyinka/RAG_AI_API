using RAG_AI_API.DTOs;
using System.Text.RegularExpressions;

namespace RAG_AI_API.Services;


public interface IRerankerService
{
    Task<IReadOnlyList<RankedChunk>> RerankAsync(string query, IReadOnlyList<DocumentChunkResult> candidates, CancellationToken cancellationToken);
}


public class RerankerService : IRerankerService
{
    public Task<IReadOnlyList<RankedChunk>> RerankAsync(string query, IReadOnlyList<DocumentChunkResult> candidates, CancellationToken cancellationToken)
    {
        var queryTerms = Tokenize(query);

        var ranked = candidates
                        .Select(candidate =>
                        {
                            var contentTerms = Tokenize(candidate.Content);

                            var overlap = queryTerms.Count == 0 ? 0 : queryTerms.Intersect(contentTerms, StringComparer.OrdinalIgnoreCase).Count() / (double)queryTerms.Count;

                            var exactPhrase = candidate.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.25 : 0;

                            var score = Math.Clamp((candidate.Similarity * 0.70) + (overlap * 0.25) + exactPhrase, 0, 1);

                            return new RankedChunk(candidate.ChunkId,
                                                   candidate.DocumentId,
                                                   candidate.DocumentName,
                                                   candidate.Content,
                                                   candidate.Heading,
                                                   candidate.PageNumber,
                                                   candidate.Similarity,
                                                   score);
                        })
                        .OrderByDescending(x => x.RerankScore)
                        .ToList();

        return Task.FromResult<IReadOnlyList<RankedChunk>>(ranked);
    }


    private static HashSet<string> Tokenize(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{Nd}]{2,}")
                                                                 .Select(x => x.Value)
                                                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
