<script>
	import Intro from '$lib/Intro.svelte';
  	import TableOfContents from '../lib/TableOfContents.svelte';
  	import Directory from '$lib/Directory.svelte';

	import yaml from 'js-yaml';
	import file from '../../DIRECTORY.yaml?raw';
	const root = yaml.load(file);
</script>

<Intro/>

<hr>
<TableOfContents root={root}/>


<Directory root={root}/>
