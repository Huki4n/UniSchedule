using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class HomeworkCommentDraftTests
{
    [Fact]
    public void Draft_KeepsAddsAndRemovesUntilSave()
    {
        var existing = new HomeworkComment
        {
            Id = 3,
            HomeworkId = 8,
            Body = "был",
            CreatedAt = new DateTime(2026, 9, 25, 10, 0, 0)
        };
        var draft = new HomeworkCommentDraft([existing]);
        var created = new DateTime(2026, 9, 25, 11, 30, 40);

        draft.Add("  новый  ", created);
        draft.Add("   ", created);

        Assert.Equal(["был", "новый"], draft.Visible.Select(comment => comment.Body));
        var added = Assert.Single(draft.Added);
        Assert.True(added.Id < 0);
        Assert.Equal("новый", added.Body);
        Assert.Equal(created, added.CreatedAt);

        draft.Remove(added.Id);
        Assert.Equal(["был"], draft.Visible.Select(comment => comment.Body));
        Assert.Empty(draft.Added);
        Assert.Empty(draft.RemovedIds);

        draft.Remove(3);
        Assert.Empty(draft.Visible);
        Assert.Equal([3L], draft.RemovedIds);
        Assert.Equal("был", existing.Body);
    }

    [Fact]
    public void Draft_HasEdits_WhenCommentAddedOrRemoved()
    {
        var existing = new HomeworkComment { Id = 3, HomeworkId = 8, Body = "был" };
        var draft = new HomeworkCommentDraft([existing]);

        Assert.False(draft.HasEdits);
        draft.Add("новый", new DateTime(2026, 9, 25, 11, 0, 0));
        Assert.True(draft.HasEdits);
        draft.Remove(draft.Added[0].Id);
        Assert.False(draft.HasEdits);
        draft.Remove(3);
        Assert.True(draft.HasEdits);
    }
}
