using System.Net;
using System.Text.Json;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers
{
    [TestFixture]
    public class AlbumControllerTests
    {
        private WebApplicationFactory<Program> _factory;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Guid _garden = Guid.NewGuid();
        private readonly Guid _holidays = Guid.NewGuid();

        private WebApplicationFactory<Program> CreateFactory(string? accountName)
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>().WithServerVersion();

            return new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.UseMockHandler(_mockHttpMessageHandler);

                        var generalSettings = new GeneralSettings
                        {
                            Language = "en",
                            PhotoDateFormat = "MM/dd/yyyy",
                            ImageLocationFormat = "City,State,Country",
                            WeatherApiKey = "",
                            WeatherLatLong = "0,0"
                        };
                        var accountSettings = new ServerAccountSettings
                        {
                            Name = accountName,
                            ImmichServerUrl = "http://mock-immich-server.com",
                            ApiKey = "test-api-key",
                            ShowMemories = false,
                            Albums = new List<Guid> { _garden, _holidays },
                            ExcludedAlbums = new List<Guid>(),
                            People = new List<Guid>()
                        };
                        var serverSettings = new ServerSettings
                        {
                            GeneralSettingsImpl = generalSettings,
                            AccountsImpl = new List<ServerAccountSettings> { accountSettings }
                        };

                        services.AddSingleton<ISettingsProvider>(new StaticSettingsProvider(serverSettings));
                        services.AddSingleton<IServerSettings>(serverSettings);
                        services.AddSingleton<IGeneralSettings>(generalSettings);
                    });
                });
        }

        private void AlbumExists(Guid id, string name)
        {
            var json = JsonSerializer.Serialize(new AlbumResponseDto { Id = id, AlbumName = name, Description = "", ContributorCounts = [] });
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.EndsWith($"/albums/{id}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                });
        }

        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();
        }

        [Test]
        public async Task GetConfiguredAlbums_NamedAccount_ReturnsItsAlbums()
        {
            _factory = CreateFactory("family");
            AlbumExists(_garden, "Garden");
            AlbumExists(_holidays, "Holidays");
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/Album?clientIdentifier=tv");

            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var albums = json.RootElement.EnumerateArray()
                .Select(a => (
                    a.GetProperty("id").GetGuid(),
                    a.GetProperty("albumName").GetString(),
                    a.GetProperty("accountName").GetString()))
                .ToList();
            Assert.That(albums, Is.EqualTo(new[]
            {
                (_garden, "Garden", "family"),
                (_holidays, "Holidays", "family")
            }));
        }

        [Test]
        public async Task GetConfiguredAlbums_UnnamedAccount_ReturnsEmptyList()
        {
            _factory = CreateFactory(null);
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/Album");

            response.EnsureSuccessStatusCode();
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("[]"));
        }
    }
}
