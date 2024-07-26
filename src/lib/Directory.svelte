<script>
	export let root;
</script>

<div>
	{#each Object.entries(root) as [category, subcategories]}
		<hr>
		<h2>{category}</h2>
		{#each Object.entries(subcategories) as [subcategory, entry]}
			<h3 id="{subcategory}">{subcategory}</h3>
			<ul>
				{#each entry as item}
					<li>
						{#if item.trusted === "yes"}
							✅
						{:else if item.trusted === "no"}
							❌
						{/if}
						<a href="{item.link}">{item.name}</a> - {item.description}

					</li>
				{/each}
			</ul>
		{/each}
	{/each}
</div>
