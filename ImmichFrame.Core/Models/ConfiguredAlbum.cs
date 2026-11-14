namespace ImmichFrame.Core.Models;

/// <summary>
/// An album from an account's configured <c>Albums</c>, with what a client needs to show only that
/// album: its name for display, and the account name and album ID to send as <c>accountName</c> and
/// <c>albumIds</c> when requesting assets.
/// </summary>
public record ConfiguredAlbum(Guid Id, string AlbumName, string AccountName);
