using UniSchedule.Models;

namespace UniSchedule.Services;

public sealed class HomeworkCommentDraft
{
    private readonly List<HomeworkComment> _visible = [];
    private readonly List<long> _removedIds = [];
    private readonly List<HomeworkComment> _added = [];
    private long _nextTempId = -1;

    public HomeworkCommentDraft(IEnumerable<HomeworkComment> existing)
    {
        foreach (var comment in existing)
        {
            _visible.Add(new HomeworkComment
            {
                Id = comment.Id,
                HomeworkId = comment.HomeworkId,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt
            });
        }
    }

    public IReadOnlyList<HomeworkComment> Visible => _visible;

    public IReadOnlyList<long> RemovedIds => _removedIds;

    public IReadOnlyList<HomeworkComment> Added => _added;

    public void Add(string? text, DateTime createdAt)
    {
        var body = (text ?? "").Trim();
        if (body.Length == 0)
        {
            return;
        }

        var comment = new HomeworkComment
        {
            Id = _nextTempId--,
            Body = body,
            CreatedAt = createdAt
        };
        _added.Add(comment);
        _visible.Add(comment);
    }

    public void Remove(long id)
    {
        var index = _visible.FindIndex(comment => comment.Id == id);
        if (index < 0)
        {
            return;
        }

        _visible.RemoveAt(index);
        if (id > 0)
        {
            _removedIds.Add(id);
            return;
        }

        _added.RemoveAll(comment => comment.Id == id);
    }
}
