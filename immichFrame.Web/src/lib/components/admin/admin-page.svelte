<script lang="ts">
	import { onDestroy, onMount, untrack } from 'svelte';
	import { get } from 'svelte/store';
	import { mdiLogout, mdiPlus } from '@mdi/js';
	import {
		Alert,
		AppShell,
		AppShellHeader,
		Button,
		Card,
		CardBody,
		Code,
		Heading,
		IconButton,
		Text
	} from '@immich/ui';
	import * as adminApi from '$lib/services/admin';
	import { createAutosave, type AutosaveState } from '$lib/autosave';
	import { AdminUiState, type ServerSettings } from '$lib/immichFrameApi';
	import { adminPasswordStore } from '$lib/stores/admin.store';
	import { generalSections } from './admin-fields';
	import AdminBrand from './admin-brand.svelte';
	import AdminLogin from './admin-login.svelte';
	import AdminSetup from './admin-setup.svelte';
	import SettingsSection from './settings-section.svelte';
	import SettingField from './setting-field.svelte';
	import AccountEditor from './account-editor.svelte';

	type PageState = 'loading' | 'setup' | 'disabled' | 'login' | 'editor' | 'error';

	let pageState: PageState = $state('loading');
	let settings: ServerSettings = $state({ General: {}, Accounts: [] });
	let loginError = $state('');
	let setupError = $state('');
	let saveState: AutosaveState = $state('idle');
	let saveError = $state('');
	let saveWarnings: string[] = $state([]);

	// Every change saves itself shortly after editing pauses. `savedSnapshot` is what the server
	// holds, so loading settings, or undoing an edit before it saves, does not trigger a save.
	let savedSnapshot = '';
	const autosave = createAutosave({ save, onStateChange: (state) => (saveState = state) });

	$effect(() => {
		const snapshot = JSON.stringify(settings);
		if (pageState !== 'editor') {
			return;
		}
		untrack(() => {
			if (snapshot !== savedSnapshot || autosave.hasUnsavedChanges) {
				autosave.schedule();
			}
		});
	});

	onDestroy(() => autosave.cancel());

	function warnBeforeLeaving(event: BeforeUnloadEvent) {
		if (pageState === 'editor' && autosave.hasUnsavedChanges) {
			void autosave.flush();
			event.preventDefault();
		}
	}

	// Remount account editors after (re)loading settings so their local list texts reset
	let accountsVersion = $state(0);

	onMount(loadStatus);

	async function loadStatus() {
		try {
			const res = await adminApi.getStatus();
			if (res.status != 200) {
				pageState = 'error';
				return;
			}
			if (res.data.state === AdminUiState.Setup) {
				pageState = 'setup';
				return;
			}
			if (res.data.state === AdminUiState.Disabled) {
				pageState = 'disabled';
				return;
			}
			if (get(adminPasswordStore)) {
				await loadSettings();
			} else {
				pageState = 'login';
			}
		} catch {
			pageState = 'error';
		}
	}

	// Claims the instance, then signs in with the password just chosen so the user
	// lands straight in the editor to add their first account.
	async function setup(password: string) {
		setupError = '';
		try {
			const res = await adminApi.setup(password);
			if (res.status != 200) {
				setupError = 'Setup failed. Check the server logs and try again.';
				return;
			}
			adminPasswordStore.set(password);
			await loadSettings();
		} catch {
			setupError = 'Setup failed. Is the server reachable?';
		}
	}

	async function loadSettings() {
		try {
			const res = await adminApi.getSettings();
			if (res.status == 200) {
				autosave.cancel();
				settings = { General: res.data.General ?? {}, Accounts: res.data.Accounts ?? [] };
				savedSnapshot = JSON.stringify(settings);
				saveError = '';
				saveWarnings = [];
				accountsVersion++;
				pageState = 'editor';
				loginError = '';
			} else {
				adminPasswordStore.set(null);
				loginError = 'Could not authenticate. Check the admin password.';
				pageState = 'login';
			}
		} catch {
			pageState = 'error';
		}
	}

	function login(password: string) {
		adminPasswordStore.set(password);
		loadSettings();
	}

	async function logout() {
		await autosave.flush();
		autosave.cancel();
		adminPasswordStore.set(null);
		loginError = '';
		pageState = 'login';
	}

	function addAccount() {
		settings.Accounts = [...(settings.Accounts ?? []), {}];
	}

	function removeAccount(index: number) {
		// Removing saves at once, so ask first: the account's settings cannot be got back.
		if (!confirm(`Remove account ${index + 1}? This saves immediately.`)) {
			return;
		}
		settings.Accounts = (settings.Accounts ?? []).filter((_, i) => i != index);
		accountsVersion++;
	}

	async function save(): Promise<boolean> {
		const body = $state.snapshot(settings) as ServerSettings;
		try {
			const res = await adminApi.updateSettings(body);
			if (res.status == 200) {
				savedSnapshot = JSON.stringify(body);
				saveError = '';
				saveWarnings = res.data.warnings ?? [];
				return true;
			} else if (res.status == 401) {
				autosave.cancel();
				adminPasswordStore.set(null);
				loginError = 'Session expired. Sign in again. Your last change was not saved.';
				pageState = 'login';
			} else {
				const data = res.data as { detail?: string } | string | null;
				saveError =
					(typeof data === 'object' && data?.detail) ||
					(typeof data === 'string' && data) ||
					'Saving failed.';
			}
		} catch {
			saveError = 'Saving failed. Is the server reachable?';
		}
		return false;
	}
</script>

<svelte:window onbeforeunload={warnBeforeLeaving} />

<AppShell class="bg-light px-4">
	{#if pageState === 'editor'}
		<AppShellHeader>
			<div class="w-full p-4">
				<div class="mx-auto flex w-full max-w-3xl items-center justify-between gap-4">
					<AdminBrand subtitle="Server settings" />

					<div class="flex items-center gap-2">
						<div role="status" aria-live="polite" class="flex items-center gap-2">
							{#if saveState === 'pending' || saveState === 'saving'}
								<Text color="muted" size="small">Saving…</Text>
							{:else if saveState === 'saved'}
								<Text color="success" size="small">Saved</Text>
							{:else if saveState === 'error'}
								<Text color="danger" size="small">Not saved</Text>
								<Button size="small" variant="outline" onclick={() => autosave.retry()}>
									Retry
								</Button>
							{:else}
								<Text color="muted" size="small">Changes save automatically</Text>
							{/if}
						</div>
						<IconButton
							icon={mdiLogout}
							variant="outline"
							color="secondary"
							aria-label="Sign out"
							title="Sign out"
							onclick={logout}
						/>
					</div>
				</div>
			</div>
		</AppShellHeader>
	{/if}
	<div class="p-4 pb-24">
		{#if pageState === 'loading'}
			<Text color="muted" class="block pt-24 text-center">Loading…</Text>
		{:else if pageState === 'error'}
			<Text color="muted" class="block pt-24 text-center">
				Could not reach the ImmichFrame server. Check the container logs and reload this page.
			</Text>
		{:else if pageState === 'setup'}
			<AdminSetup error={setupError} onsetup={setup} />
		{:else if pageState === 'disabled'}
			<Card class="mx-auto mt-24 max-w-lg">
				<CardBody class="text-center">
					<Heading size="large" class="mb-4">Admin UI is disabled</Heading>
					<Text color="muted">
						This instance is already configured but has no admin password. Set the
						<Code>IMMICHFRAME_ADMIN_PASSWORD</Code> environment variable and restart the container to
						get in.
					</Text>
				</CardBody>
			</Card>
		{:else if pageState === 'login'}
			<AdminLogin error={loginError} onlogin={login} />
		{:else}
			<div class="mx-auto max-w-3xl">
				<Text color="muted" size="small" class="mb-4 block">
					Changes save automatically and apply live. Slideshow devices pick up display changes on
					their next reload; weather and calendars refresh within ~15 minutes.
				</Text>
				{#if saveWarnings.length}
					<Alert color="warning" title="Saved, with warnings" class="mb-4">
						<ul class="list-disc pl-5">
							{#each saveWarnings as warning (warning)}
								<li>{warning}</li>
							{/each}
						</ul>
					</Alert>
				{/if}
				{#if saveError}
					<Alert color="danger" class="mb-4">{saveError}</Alert>
				{/if}

				<div class="flex flex-col gap-4">
					{#each generalSections as section (section.title)}
						<SettingsSection title={section.title} open={section.title === 'Display'}>
							<div class="grid gap-x-8 sm:grid-cols-2">
								{#each section.fields as field (field.key)}
									<SettingField {field} target={settings.General ?? {}} />
								{/each}
							</div>
						</SettingsSection>
					{/each}

					<SettingsSection title="Accounts">
						<div class="flex flex-col gap-4">
							{#key accountsVersion}
								{#each settings.Accounts ?? [] as account, index (index)}
									<AccountEditor {account} {index} onremove={() => removeAccount(index)} />
								{/each}
							{/key}
							{#if !(settings.Accounts ?? []).length}
								<Text color="muted" size="small">
									No accounts configured yet — the slideshow has nothing to show. Add your first
									Immich account below.
								</Text>
							{/if}
							<Button
								leadingIcon={mdiPlus}
								variant="outline"
								color="secondary"
								size="small"
								class="w-fit"
								onclick={addAccount}
							>
								Add account
							</Button>
						</div>
					</SettingsSection>
				</div>
			</div>
		{/if}
	</div>
</AppShell>
