using CommonVisionNodes.Server.Services;

namespace CommonVisionNodes.Test;

public sealed class ServerBindingValidatorTests
{
    [TestCase("http://localhost:5077")]
    [TestCase("http://127.0.0.1:5077")]
    [TestCase("http://[::1]:5077")]
    [TestCase("http://localhost:5077;https://127.0.0.1:5078")]
    public void EnsureLoopbackUrls_WithLoopbackUrls_ShouldSucceed(string urls)
    {
        Assert.DoesNotThrow(() => ServerBindingValidator.EnsureLoopbackUrls(urls));
    }

    [TestCase("http://0.0.0.0:5077")]
    [TestCase("http://192.168.1.10:5077")]
    [TestCase("http://*:5077")]
    [TestCase("not-a-url")]
    [TestCase("")]
    public void EnsureLoopbackUrls_WithRemoteOrInvalidUrl_ShouldThrow(string urls)
    {
        Assert.Throws<InvalidOperationException>(() => ServerBindingValidator.EnsureLoopbackUrls(urls));
    }
}
