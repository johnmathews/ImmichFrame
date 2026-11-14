using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic;

/// <summary>
/// Looks up the names of an account's configured albums, so a client can offer them one at a time.
/// Only named accounts take part: a client selects albums with <c>accountName</c> and <c>albumIds</c>,
/// and an account without a name cannot be selected.
/// </summary>
public class ConfiguredAlbumsResolver(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger logger)
{
    public async Task<IReadOnlyList<ConfiguredAlbum>> GetAlbums()
    {
        var accountName = accountSettings.Name?.Trim();
        if (string.IsNullOrEmpty(accountName) || accountSettings.Albums is not { Count: > 0 } albumIds)
        {
            return [];
        }

        var albums = new List<ConfiguredAlbum>();
        foreach (var albumId in albumIds.Distinct())
        {
            try
            {
                var album = await apiCache.GetOrAddAsync($"configured-album-{albumId}",
                    () => immichApi.GetAlbumInfoAsync(albumId, null, null));
                albums.Add(new ConfiguredAlbum(albumId, album.AlbumName, accountName));
            }
            catch (ApiException ex)
            {
                // A deleted album, or one the API key cannot read, would show nothing; leave it out.
                logger.LogWarning("Configured album {albumId} of account '{account}' could not be read (HTTP {status}); it is not offered to clients",
                    albumId, accountName.SanitizeString(), ex.StatusCode);
            }
        }

        return albums;
    }
}
