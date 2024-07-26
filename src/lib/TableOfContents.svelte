<script>
	export let root;
</script>

<div class="table-of-contents">
	<h2>Table of Contents</h2>
	<ul class="tree">
		{#each Object.entries(root) as [category, subcategories]}
			<li class="section">
				<input type="checkbox" id={category} />
				<label for={category}>{category}</label>
				<ul>
					{#each Object.entries(subcategories) as [subcategory, entry]}
						<li><a href="#{subcategory}">{subcategory}</a></li>
					{/each}
				</ul>
			</li>
		{/each}
	</ul>
</div>

<style>
	.tree,
	.section ul {
		list-style: none;
		padding: 0;
		margin: 10;
	}

	.tree li {
		border-bottom: 0px solid #d2d2d2;
		padding: 0px 20px;
	}
	.tree li:last-child {
		border: 0;
	}

	/* (B) SUB-SECTIONS */

	/* (B1) TOGGLE SHOW/HIDE */
	.section ul {
		display: none;
	}
	.section input:checked ~ ul {
		display: block;
	}

	/* (B2) HIDE CHECKBOX */
	.section input[type='checkbox'] {
		display: none;
	}

	/* (B3) ADD EXPAND/COLLAPSE ICON  */
	.section {
		position: relative;
		padding-left: 35px !important;
	}
	.section label:after {
		content: '\0002B';
		position: absolute;
		left: 0;
		padding-left: 5px;
		text-align: center;
		color: lightblue;
		transition: all 0.5s;
	}
	.section input:checked ~ label:after {
		color: #23c37a;
		transform: rotate(45deg);
	}

	/* (B4) SUB-SECTION ITEMS */
	.section ul {
		margin-top: 5px;
	}
	.section ul li {
		color: #008cff;
	}

	/* (X) DOES NOT REALLY MATTER */
	.tree,
	.tree * {
		font-size: 20px;
		box-sizing: border-box;
	}
</style>
