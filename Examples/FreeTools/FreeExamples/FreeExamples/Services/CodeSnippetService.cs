using System.Collections.Concurrent;

namespace FreeExamples.Server.Services;

/// <summary>
/// In-memory service for Code Playground snippet storage.
/// Same pattern as ApiKeyDemoService — ConcurrentDictionary, no database.
/// </summary>
public class CodeSnippetService
{
    private readonly ConcurrentDictionary<Guid, DataObjects.CodeSnippet> _snippets = new();

    /// <summary>
    /// SaveMany pattern: save a snippet (insert or update).
    /// </summary>
    public DataObjects.CodeSnippet SaveSnippet(DataObjects.CodeSnippet snippet)
    {
        if (snippet.SnippetId == Guid.Empty) {
            snippet.SnippetId = Guid.NewGuid();
        }
        snippet.LastSaved = DateTime.UtcNow;
        _snippets[snippet.SnippetId] = snippet;
        return snippet;
    }

    /// <summary>
    /// GetMany pattern: null/empty → all, list of IDs → filtered.
    /// </summary>
    public List<DataObjects.CodeSnippet> GetSnippets(List<Guid>? ids = null)
    {
        if (ids == null || ids.Count == 0) {
            return _snippets.Values.OrderByDescending(s => s.LastSaved).ToList();
        }
        return _snippets.Values.Where(s => ids.Contains(s.SnippetId)).OrderByDescending(s => s.LastSaved).ToList();
    }

    /// <summary>
    /// DeleteMany pattern: delete by IDs.
    /// </summary>
    public bool DeleteSnippet(Guid id)
    {
        return _snippets.TryRemove(id, out _);
    }
}
