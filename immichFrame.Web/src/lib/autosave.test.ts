import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createAutosave, type AutosaveState } from './autosave';

function deferred() {
	let resolve!: (ok: boolean) => void;
	const promise = new Promise<boolean>((r) => (resolve = r));
	return { promise, resolve };
}

describe('createAutosave', () => {
	let states: AutosaveState[];

	beforeEach(() => {
		vi.useFakeTimers();
		states = [];
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it('saves once, after the delay, however many changes come in', async () => {
		const save = vi.fn().mockResolvedValue(true);
		const autosave = createAutosave({ save, delayMs: 800, onStateChange: (s) => states.push(s) });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(500);
		autosave.schedule();
		await vi.advanceTimersByTimeAsync(500);
		expect(save).not.toHaveBeenCalled();

		await vi.advanceTimersByTimeAsync(300);
		expect(save).toHaveBeenCalledTimes(1);
		expect(autosave.state).toBe('saved');
		expect(states).toEqual(['pending', 'saving', 'saved']);
	});

	it('reports an error when the save fails, and saves again on the next change', async () => {
		const save = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(autosave.state).toBe('error');
		expect(autosave.hasUnsavedChanges).toBe(true);

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(save).toHaveBeenCalledTimes(2);
		expect(autosave.state).toBe('saved');
		expect(autosave.hasUnsavedChanges).toBe(false);
	});

	it('treats a thrown error as a failed save', async () => {
		const save = vi.fn().mockRejectedValue(new Error('offline'));
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(autosave.state).toBe('error');
	});

	it('never runs two saves at once, and saves again for a change made mid-save', async () => {
		const first = deferred();
		const save = vi.fn().mockReturnValueOnce(first.promise).mockResolvedValueOnce(true);
		const autosave = createAutosave({ save, delayMs: 100, onStateChange: (s) => states.push(s) });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(save).toHaveBeenCalledTimes(1);

		// A change while the first save is still running: its timer fires before the save ends.
		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(save).toHaveBeenCalledTimes(1);
		expect(autosave.hasUnsavedChanges).toBe(true);

		first.resolve(true);
		await vi.advanceTimersByTimeAsync(0);
		expect(save).toHaveBeenCalledTimes(2);
		expect(autosave.state).toBe('saved');
		expect(states.at(-1)).toBe('saved');
	});

	it('stays pending when a change arrives mid-save and its timer has not fired yet', async () => {
		const first = deferred();
		const save = vi.fn().mockReturnValueOnce(first.promise).mockResolvedValueOnce(true);
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		autosave.schedule();
		first.resolve(true);
		await vi.advanceTimersByTimeAsync(0);
		expect(autosave.state).toBe('pending');

		await vi.advanceTimersByTimeAsync(100);
		expect(save).toHaveBeenCalledTimes(2);
		expect(autosave.state).toBe('saved');
	});

	it('flush saves a pending change at once', async () => {
		const save = vi.fn().mockResolvedValue(true);
		const autosave = createAutosave({ save, delayMs: 10_000 });

		autosave.schedule();
		await autosave.flush();
		expect(save).toHaveBeenCalledTimes(1);
		expect(autosave.state).toBe('saved');

		await vi.advanceTimersByTimeAsync(10_000);
		expect(save).toHaveBeenCalledTimes(1);
	});

	it('flush with nothing pending does not save', async () => {
		const save = vi.fn().mockResolvedValue(true);
		const autosave = createAutosave({ save });

		await autosave.flush();
		expect(save).not.toHaveBeenCalled();
		expect(autosave.state).toBe('idle');
	});

	it('flush waits for a save that is already running', async () => {
		const first = deferred();
		const save = vi.fn().mockReturnValueOnce(first.promise);
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		let flushed = false;
		const flushing = autosave.flush().then(() => (flushed = true));
		await vi.advanceTimersByTimeAsync(0);
		expect(flushed).toBe(false);

		first.resolve(true);
		await flushing;
		expect(save).toHaveBeenCalledTimes(1);
	});

	it('retry saves again after a failure without a new change', async () => {
		const save = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		await vi.advanceTimersByTimeAsync(100);
		expect(autosave.state).toBe('error');

		await autosave.retry();
		expect(save).toHaveBeenCalledTimes(2);
		expect(autosave.state).toBe('saved');
	});

	it('cancel drops a pending save and returns to idle', async () => {
		const save = vi.fn().mockResolvedValue(true);
		const autosave = createAutosave({ save, delayMs: 100 });

		autosave.schedule();
		autosave.cancel();
		await vi.advanceTimersByTimeAsync(200);
		expect(save).not.toHaveBeenCalled();
		expect(autosave.state).toBe('idle');
		expect(autosave.hasUnsavedChanges).toBe(false);
	});
});
