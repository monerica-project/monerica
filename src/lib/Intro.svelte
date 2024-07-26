<script>
	function get_date() {
		// remember, our Javascript runs at build time
		const date = new Date();
		const time = date.toUTCString().replace('GMT', 'UTC+00:00');
		return time;
	}
</script>

<div class="intro">
	<h1>Welcome To Monerica!</h1>
	<h4>A directory for a Monero circular economy</h4>
	<img class="logo" src="logo.png" alt="Monerica Logo" />
	<p>
		The goal of Monerica is to reflect the freedom ideals of the United States of America in the age
		of cryptocurrency by using Monero. There are no borders to Monerica. One has the ability to
		transact freely and privately without the need for a bank account, without the permission of any
		third party, and without the prying eyes of those who wish to control the thoughts and behaviors
		of others through surveillance and overreaching regulations.
	</p>

	<h4>Last Updated: {get_date()}</h4>
	<p>
		Note: The provided links are not endorsements
		<br />
		✅ - indicates someone in the community has done a successful test transaction or has used this software
		service
		<br />
		❌ - indicates someone in the community has reported this a scam
	</p>
</div>

<style>
	.logo {
		width: 333px;
	}
</style>
