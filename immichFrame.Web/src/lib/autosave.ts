/**
 * Saves after edits stop for a moment, so the admin page needs no Save button.
 *
 * - `pending`: a change is waiting for its delay to pass.
 * - `saving`: a request is running. Only one runs at a time; a change made meanwhile is saved
 *   once it ends.
 * - `saved` / `error`: the outcome of the last save.
 */
export type AutosaveState = 'idle' | 'pending' | 'saving' | 'saved' | 'error';

export interface AutosaveOptions {
	/** Sends the current settings. Resolves true when the server stored them. */
	save: () => Promise<boolean>;
	/** How long edits must pause before a save starts. */
	delayMs?: number;
	onStateChange?: (state: AutosaveState) => void;
}

export interface Autosave {
	readonly state: AutosaveState;
	/** True while a change has not reached the server, including after a failed save. */
	readonly hasUnsavedChanges: boolean;
	/** Call after every change. */
	schedule(): void;
	/** Saves a pending change now, and waits for any save already running. */
	flush(): Promise<void>;
	/** Saves again after a failure, without waiting for a new change. */
	retry(): Promise<void>;
	/** Drops a pending change, e.g. when the settings are reloaded or the admin signs out. */
	cancel(): void;
}

export const AUTOSAVE_DELAY_MS = 800;

export function createAutosave({
	save,
	delayMs = AUTOSAVE_DELAY_MS,
	onStateChange
}: AutosaveOptions): Autosave {
	let state: AutosaveState = 'idle';
	let timer: ReturnType<typeof setTimeout> | undefined;
	let running: Promise<void> | null = null;
	// A change whose delay ended while a save was running.
	let rerun = false;

	function setState(next: AutosaveState) {
		if (next !== state) {
			state = next;
			onStateChange?.(next);
		}
	}

	function run(): Promise<void> {
		timer = undefined;
		if (running) {
			rerun = true;
			return running;
		}
		setState('saving');
		running = (async () => {
			let ok: boolean;
			try {
				ok = await save();
			} catch {
				ok = false;
			}
			running = null;
			if (rerun) {
				rerun = false;
				await run();
			} else if (timer !== undefined) {
				setState('pending');
			} else {
				setState(ok ? 'saved' : 'error');
			}
		})();
		return running;
	}

	return {
		get state() {
			return state;
		},
		get hasUnsavedChanges() {
			return timer !== undefined || running !== null || rerun || state === 'error';
		},
		schedule() {
			clearTimeout(timer);
			timer = setTimeout(run, delayMs);
			if (!running) {
				setState('pending');
			}
		},
		async flush() {
			if (timer !== undefined) {
				clearTimeout(timer);
				await run();
			} else if (running) {
				await running;
			}
		},
		async retry() {
			clearTimeout(timer);
			await run();
		},
		cancel() {
			clearTimeout(timer);
			timer = undefined;
			rerun = false;
			if (!running) {
				setState('idle');
			}
		}
	};
}
