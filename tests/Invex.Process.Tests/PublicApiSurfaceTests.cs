namespace Invex.Process.Tests;

[TestFixture]
public class PublicApiTests
{
    [Test]
    public async Task VerifyPublicApiSurface() =>
        await VerifyJson(PublicApiSurfaceTestUtil.GetPublicApiSurface(typeof(ProcessRunner).Assembly));
}
