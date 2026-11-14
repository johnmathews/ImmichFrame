using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class ConfiguredAlbumsResolverTests
{
    private Mock<IApiCache> _cache;
    private Mock<ImmichApi> _api;
    private Mock<IAccountSettings> _settings;

    [SetUp]
    public void Setup()
    {
        _cache = new Mock<IApiCache>();
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<Func<Task<AlbumResponseDto>>>()))
            .Returns<string, Func<Task<AlbumResponseDto>>>((_, factory) => factory());
        _api = new Mock<ImmichApi>(null, null);
        _settings = new Mock<IAccountSettings>();
        _settings.SetupGet(s => s.Name).Returns("family");
    }

    private ConfiguredAlbumsResolver CreateResolver(params Guid[] albums)
    {
        _settings.SetupGet(s => s.Albums).Returns(albums.ToList());
        return new ConfiguredAlbumsResolver(_cache.Object, _api.Object, _settings.Object, NullLogger.Instance);
    }

    private void AlbumExists(Guid id, string name) =>
        _api.Setup(a => a.GetAlbumInfoAsync(id, null, null))
            .ReturnsAsync(new AlbumResponseDto { Id = id, AlbumName = name });

    [Test]
    public async Task GetAlbums_NamedAccount_ReturnsAlbumsInConfiguredOrder()
    {
        var holidays = Guid.NewGuid();
        var garden = Guid.NewGuid();
        AlbumExists(holidays, "Holidays");
        AlbumExists(garden, "Garden");

        var albums = await CreateResolver(holidays, garden).GetAlbums();

        Assert.That(albums.Select(a => (a.Id, a.AlbumName, a.AccountName)), Is.EqualTo(new[]
        {
            (holidays, "Holidays", "family"),
            (garden, "Garden", "family")
        }));
    }

    [TestCase(null)]
    [TestCase("  ")]
    public async Task GetAlbums_UnnamedAccount_ReturnsNothing(string? name)
    {
        _settings.SetupGet(s => s.Name).Returns(name);

        var albums = await CreateResolver(Guid.NewGuid()).GetAlbums();

        Assert.That(albums, Is.Empty);
        _api.Verify(a => a.GetAlbumInfoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GetAlbums_UnreadableAlbum_IsLeftOut()
    {
        var deleted = Guid.NewGuid();
        var garden = Guid.NewGuid();
        _api.Setup(a => a.GetAlbumInfoAsync(deleted, null, null))
            .ThrowsAsync(new ApiException("Not found", 404, "", new Dictionary<string, IEnumerable<string>>(), null));
        AlbumExists(garden, "Garden");

        var albums = await CreateResolver(deleted, garden).GetAlbums();

        Assert.That(albums.Select(a => a.Id), Is.EqualTo(new[] { garden }));
    }

    [Test]
    public async Task GetAlbums_DuplicateAlbum_IsListedOnce()
    {
        var garden = Guid.NewGuid();
        AlbumExists(garden, "Garden");

        var albums = await CreateResolver(garden, garden).GetAlbums();

        Assert.That(albums.Select(a => a.Id), Is.EqualTo(new[] { garden }));
    }

    [Test]
    public async Task GetAlbums_UsesTheCachePerAlbum()
    {
        var garden = Guid.NewGuid();
        AlbumExists(garden, "Garden");

        await CreateResolver(garden).GetAlbums();

        _cache.Verify(c => c.GetOrAddAsync($"configured-album-{garden}", It.IsAny<Func<Task<AlbumResponseDto>>>()), Times.Once);
    }
}
