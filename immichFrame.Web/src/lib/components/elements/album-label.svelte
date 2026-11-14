<script lang="ts">
	interface Props {
		label: string;
	}

	let { label }: Props = $props();
</script>

<!-- Plain scoped CSS rather than utility classes, so the label also renders in old TV browsers. -->
<div class="album-label" role="status" aria-live="polite">{label}</div>

<style>
	.album-label {
		position: fixed;
		top: 2rem;
		left: 50%;
		transform: translateX(-50%);
		z-index: 200;
		max-width: 80vw;
		padding: 0.5em 1.25em;
		border-radius: 0.5em;
		background: rgba(0, 0, 0, 0.6);
		color: #fff;
		font-size: 2rem;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
		pointer-events: none;
	}
</style>
