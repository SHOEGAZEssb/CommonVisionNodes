using Cvb.Uno.Toolkit.Helpers;

namespace CommonVisionNodes.Test;

public sealed class CoalescingMessageBufferTests
{
    [Test]
    public void AddOrReplace_KeepsOnlyLatestValuePerKey()
    {
        var buffer = new CoalescingMessageBuffer<string, int>();

        Assert.That(buffer.AddOrReplace("node-a", 1), Is.True);
        Assert.That(buffer.AddOrReplace("node-a", 2), Is.False);
        Assert.That(buffer.AddOrReplace("node-b", 3), Is.False);

        var batch = buffer.TakeBatch(10, out var requiresAnotherDrain);

        Assert.Multiple(() =>
        {
            Assert.That(batch, Is.EquivalentTo(new[] { 2, 3 }));
            Assert.That(requiresAnotherDrain, Is.False);
        });
    }

    [Test]
    public void TakeBatch_BoundsEachDrainAndRequestsAnother()
    {
        var buffer = new CoalescingMessageBuffer<int, string>();
        for (var index = 0; index < 5; index++)
            buffer.AddOrReplace(index, $"value-{index}");

        var firstBatch = buffer.TakeBatch(2, out var requiresAnotherDrain);
        var secondBatch = buffer.TakeBatch(10, out var requiresThirdDrain);

        Assert.Multiple(() =>
        {
            Assert.That(firstBatch, Has.Count.EqualTo(2));
            Assert.That(requiresAnotherDrain, Is.True);
            Assert.That(secondBatch, Has.Count.EqualTo(3));
            Assert.That(requiresThirdDrain, Is.False);
            Assert.That(buffer.AddOrReplace(10, "next"), Is.True);
        });
    }

    [Test]
    public void Clear_DropsPendingValuesWithoutInvalidatingScheduledDrain()
    {
        var buffer = new CoalescingMessageBuffer<string, int>();
        buffer.AddOrReplace("node", 1);

        buffer.Clear();
        Assert.That(buffer.AddOrReplace("node", 2), Is.False);

        var batch = buffer.TakeBatch(10, out var requiresAnotherDrain);

        Assert.Multiple(() =>
        {
            Assert.That(batch, Is.EqualTo(new[] { 2 }));
            Assert.That(requiresAnotherDrain, Is.False);
        });
    }
}
