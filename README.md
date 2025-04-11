<p align="center">
		<img src="https://raw.githubusercontent.com/colgatto/Anonitium/refs/heads/master/DnsServerCore/www/img/logo.png" alt="Anonitium DNS Server" /><br />
		<b>Anonitium DNS Server</b><br />
		<b>Anonymous by Design</b><br />
	<br />
	<b>Based on <a href="https://technitium.com/dns/" target="_blank">Technitium DNS Server</a></b>
</p>

Anonitium is a fork of Technitium DNS Server, is basically the same but more focused on privacy and anonymity.

# Changes

- Client IPs, requested domains and answers are wipe out from logs (even if "Log All Queries" is enable)
- Top Clients, Top Domains and Top Blocked Domains are removed, even from stat file on the host machine
- Cache is stored only on memory, the `cache.bin` file no longer exists (if the process is closed all cache vanish)
- Cache tab is removed from admin web page, the api calls for cache list are removed

# Small changes

- update chart.js to v4
- web dashboard restyled

# Build Instructions
You can build the DNS server from source and install it manually by following the [Build Instructions](https://github.com/colgatto/Anonitium/blob/master/build.md).

---

For docs and every other info see <a href="https://technitium.com/dns/" target="_blank">Technitium DNS Server</a>