<script>
	// markdown conversion library and the raw markdown
	import { marked } from 'marked';
	import markdown from './../DIRECTORY.md?raw';
</script>

<div class="intro">
	<h1>Welcome To Monerica!</h1>
	<p>
		The goal of Monerica is to reflect the freedom ideals of the United States of America in the age
		of cryptocurrency by using Monero. There are no borders to Monerica. One has the ability to tra
		nsact freely and privately without the need for a bank account, without the permission of any th
		ird party, and without the prying eyes of those who wish to control the thoughts and behaviors o
        f others through surveillance and overreaching regulations.
	</p>
</div>
<p>Visit <a href="https://kit.svelte.dev">kit.svelte.dev</a> to read the documentation</p>

<div>
	{@html marked(markdown)}
</div>
