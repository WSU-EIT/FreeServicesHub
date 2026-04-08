using System.Collections.Concurrent;

namespace FreeExamples.Server.Services;

/// <summary>
/// In-memory service for Comment Thread demo.
/// Same pattern as ApiKeyDemoService — ConcurrentDictionary, no database.
/// </summary>
public class CommentService
{
    private readonly ConcurrentDictionary<Guid, DataObjects.SampleComment> _comments = new();

    /// <summary>
    /// Get comments for a specific SampleItem, ordered by Created ascending (chat style).
    /// </summary>
    public List<DataObjects.SampleComment> GetComments(Guid sampleItemId)
    {
        return _comments.Values
            .Where(c => c.SampleItemId == sampleItemId && !c.Deleted)
            .OrderBy(c => c.Created)
            .ToList();
    }

    /// <summary>
    /// Save a comment (insert or update).
    /// </summary>
    public DataObjects.SampleComment SaveComment(DataObjects.SampleComment comment)
    {
        if (comment.CommentId == Guid.Empty) {
            comment.CommentId = Guid.NewGuid();
            comment.Created = DateTime.UtcNow;
        } else {
            comment.Edited = DateTime.UtcNow;
        }

        _comments[comment.CommentId] = comment;
        return comment;
    }

    /// <summary>
    /// Delete a comment (soft delete).
    /// </summary>
    public bool DeleteComment(Guid commentId)
    {
        if (_comments.TryGetValue(commentId, out var comment)) {
            comment.Deleted = true;
            return true;
        }
        return false;
    }
}
