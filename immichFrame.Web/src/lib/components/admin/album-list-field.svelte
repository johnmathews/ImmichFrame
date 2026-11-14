<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { mdiArrowUp, mdiClose, mdiRefresh } from '@mdi/js';
	import { Button, Field, HelperText, IconButton, Input, Text, Textarea } from '@immich/ui';
	import * as adminApi from '$lib/services/admin';
	import type { AccountAlbumDto, ServerAccountSettings } from '$lib/immichFrameApi';
	import type { AnyFieldDef } from './admin-fields';

	interface Props {
		field: AnyFieldDef;
		// The account being edited; its server and key are used to list the albums.
		target: Record<string, unknown>;
	}

	let { field, target }: Props = $props();

	const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
	const MAX_MATCHES = 8;

	// Initial value only, like the other list fields: the parent remounts on settings reload.
	let ids: string[] = $state(
		untrack(() => (Array.isArray(target[field.key]) ? [...(target[field.key] as string[])] : []))
	);
	let albums = $state<AccountAlbumDto[] | null>(null);
	let loading = $state(false);
	let loadError = $state('');
	let query = $state('');
	let editingIds = $state(false);
	let idText = $state('');
	let invalidIds: string[] = $state([]);

	let albumsById = $derived(
		new Map<string, AccountAlbumDto>(
			(albums ?? []).map((album) => [album.id?.toLowerCase() ?? '', album])
		)
	);

	let matches = $derived.by(() => {
		const search = query.trim().toLowerCase();
		if (!search || !albums) {
			return [];
		}
		const chosen = new Set(ids.map((id) => id.toLowerCase()));
		return albums
			.filter(
				(album) =>
					album.id &&
					!chosen.has(album.id.toLowerCase()) &&
					(album.albumName ?? '').toLowerCase().includes(search)
			)
			.slice(0, MAX_MATCHES);
	});

	function setIds(next: string[]) {
		ids = next;
		target[field.key] = next;
	}

	function albumLabel(id: string) {
		const album = albumsById.get(id.toLowerCase());
		if (album) {
			return album.albumName ?? id;
		}
		return albums ? `Unknown album (${id})` : id;
	}

	async function loadAlbums() {
		loading = true;
		loadError = '';
		try {
			const res = await adminApi.getAccountAlbums($state.snapshot(target) as ServerAccountSettings);
			if (res.status == 200) {
				albums = res.data;
			} else if (res.status == 400) {
				loadError = res.data.detail ?? 'Could not load the albums.';
			} else {
				loadError = 'Could not load the albums.';
			}
		} catch {
			loadError = 'Could not load the albums.';
		} finally {
			loading = false;
		}
	}

	function add(id: string) {
		setIds([...ids, id]);
		query = '';
	}

	function remove(index: number) {
		setIds(ids.filter((_, i) => i !== index));
	}

	function moveUp(index: number) {
		const next = [...ids];
		[next[index - 1], next[index]] = [next[index], next[index - 1]];
		setIds(next);
	}

	function toggleIdEditing() {
		if (!editingIds) {
			idText = ids.join('\n');
			invalidIds = [];
		}
		editingIds = !editingIds;
	}

	function updateIdText(text: string) {
		idText = text;
		const lines = text
			.split('\n')
			.map((line) => line.trim())
			.filter((line) => line.length > 0);
		invalidIds = lines.filter((line) => !guidPattern.test(line));
		setIds(lines);
	}

	onMount(() => {
		if (target.immichServerUrl) {
			loadAlbums();
		}
	});
</script>

<Field label={field.label} invalid={invalidIds.length > 0}>
	{#if editingIds}
		<Textarea rows={4} value={idText} oninput={(e) => updateIdText(e.currentTarget.value)} />
	{:else}
		<div class="flex flex-col gap-2">
			{#if ids.length}
				<ol class="flex flex-col gap-1">
					{#each ids as id, index (id + index)}
						<li class="flex items-center gap-1 rounded-lg bg-subtle py-1 ps-3 pe-1">
							<span class="grow truncate" title={id}>{albumLabel(id)}</span>
							<IconButton
								icon={mdiArrowUp}
								aria-label="Move up"
								size="small"
								variant="ghost"
								color="secondary"
								shape="round"
								disabled={index === 0}
								onclick={() => moveUp(index)}
							/>
							<IconButton
								icon={mdiClose}
								aria-label="Remove"
								size="small"
								variant="ghost"
								color="secondary"
								shape="round"
								onclick={() => remove(index)}
							/>
						</li>
					{/each}
				</ol>
			{/if}

			<Input
				placeholder={albums ? 'Add an album: type part of its name' : 'Album names not loaded'}
				disabled={!albums}
				value={query}
				oninput={(e) => (query = e.currentTarget.value)}
			/>
			{#if matches.length}
				<ul class="flex flex-col overflow-hidden rounded-lg border border-light-300">
					{#each matches as album (album.id)}
						<li>
							<button
								type="button"
								class="w-full px-3 py-1 text-start hover:bg-subtle"
								onclick={() => add(album.id!)}
							>
								{album.albumName}
								<Text inline color="muted" size="small">({album.assetCount})</Text>
							</button>
						</li>
					{/each}
				</ul>
			{:else if query.trim() && albums}
				<Text color="muted" size="small">No other album matches "{query.trim()}".</Text>
			{/if}
		</div>
	{/if}

	{#if invalidIds.length}
		<HelperText color="danger">Not a valid ID: {invalidIds.join(', ')}</HelperText>
	{:else if loadError}
		<HelperText color="danger">{loadError}</HelperText>
	{:else if field.help}
		<HelperText>{field.help}</HelperText>
	{/if}

	<div class="mt-1 flex gap-2">
		<Button size="tiny" variant="ghost" color="secondary" onclick={toggleIdEditing}>
			{editingIds ? 'Choose by name' : 'Edit IDs'}
		</Button>
		<Button
			size="tiny"
			variant="ghost"
			color="secondary"
			leadingIcon={mdiRefresh}
			{loading}
			onclick={loadAlbums}
		>
			Reload album names
		</Button>
	</div>
</Field>
