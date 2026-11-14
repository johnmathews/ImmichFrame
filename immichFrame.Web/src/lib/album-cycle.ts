import type { ConfiguredAlbum } from '$lib/immichFrameApi';
import type { AssetSelection } from '$lib/stores/asset-selection.store';

/** One step of the album cycle: everything the frame shows by default, or one configured album. */
export type AlbumStop = {
	key: string;
	label: string;
	selection: AssetSelection;
};

export const ALL_ALBUMS_KEY = 'all';

/**
 * The stops ArrowUp/ArrowDown move through: first the frame's own selection ("All albums"), then
 * each configured album on its own. Returns no stops when there is nothing to switch between, or
 * when the URL already picks albums with `albums=`, which the cycle must not override.
 */
export function buildAlbumStops(base: AssetSelection, albums: ConfiguredAlbum[]): AlbumStop[] {
	if (base.albumIds) {
		return [];
	}

	const account = base.accountName?.toLowerCase();
	const single = albums
		.filter((album) => album.id && album.accountName)
		.filter((album) => !account || album.accountName!.toLowerCase() === account)
		.map((album) => ({
			key: album.id!,
			label: album.albumName || album.id!,
			selection: { accountName: album.accountName!, albumIds: album.id! }
		}));

	if (single.length < 2) {
		return [];
	}

	return [{ key: ALL_ALBUMS_KEY, label: 'All albums', selection: base }, ...single];
}

/** Index of the stop `step` places away from `index`, wrapping around at both ends. */
export function stepAlbumIndex(index: number, step: number, count: number): number {
	return (((index + step) % count) + count) % count;
}

function storageKey(clientIdentifier: string) {
	return `albumStop:${clientIdentifier}`;
}

/** The stop this client showed last, so the frame opens on it again. Storage may be unavailable. */
export function loadSavedAlbumStop(clientIdentifier: string): string | null {
	try {
		return localStorage.getItem(storageKey(clientIdentifier));
	} catch {
		return null;
	}
}

export function saveAlbumStop(clientIdentifier: string, key: string) {
	try {
		localStorage.setItem(storageKey(clientIdentifier), key);
	} catch {
		// Without storage the frame still switches; it just opens on "All albums" next time.
	}
}
