using UniSchedule.Services;

namespace UniSchedule.Tests;

public class SubjectRollbackPolicyTests
{
    [Fact]
    public void Decide_RemembersFirstNameAndForgetsWhenItReturns()
    {
        Assert.Equal(SubjectRollbackDecision.Keep, SubjectRollbackPolicy.Decide("Сети", null, " сети "));
        Assert.Equal(SubjectRollbackDecision.Remember, SubjectRollbackPolicy.Decide("Сети", null, "Базы"));
        Assert.Equal("Сети", SubjectRollbackPolicy.OriginalName("Сети", null));

        Assert.Equal(SubjectRollbackDecision.Remember, SubjectRollbackPolicy.Decide("Базы", "Сети", "Алгоритмы"));
        Assert.Equal("Сети", SubjectRollbackPolicy.OriginalName("Базы", "Сети"));

        Assert.Equal(SubjectRollbackDecision.Forget, SubjectRollbackPolicy.Decide("Алгоритмы", "Сети", "сети"));
    }
}
