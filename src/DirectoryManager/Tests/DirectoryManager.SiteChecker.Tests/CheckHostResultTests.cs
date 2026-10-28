using DirectoryManager.SiteChecker.Helpers;

namespace DirectoryManager.SiteChecker.Tests
{
    // The SiteChecker runs unattended every week and its verdicts become pending "offline"
    // submissions. check-host is the second opinion asked before flagging a site that this server
    // couldn't reach, so a parsing mistake here means false offline flags.
    public class CheckHostResultTests
    {
        // Each node: [[success, time, message, statusCode, ip]], or null while it hasn't reported.
        [Fact]
        public void Any_node_up_is_online()
        {
            const string json = """{"us1":[[0,1.2,"timeout",null,null]],"de1":[[1,0.3,"OK","200","1.2.3.4"]]}""";
            Assert.Equal(CheckOutcome.Online, CheckHostClient.InterpretResults(json));
        }

        [Fact]
        public void Redirect_status_counts_as_up()
        {
            const string json = """{"fr1":[[1,0.3,"Moved","301","1.2.3.4"]]}""";
            Assert.Equal(CheckOutcome.Online, CheckHostClient.InterpretResults(json));
        }

        [Fact]
        public void All_nodes_reported_none_up_is_offline()
        {
            const string json = """{"us1":[[0,1.2,"timeout",null,null]],"de1":[[1,0.3,"Error","503","1.2.3.4"]]}""";
            Assert.Equal(CheckOutcome.Offline, CheckHostClient.InterpretResults(json));
        }

        [Fact]
        public void A_node_still_pending_is_inconclusive_not_offline()
        {
            const string json = """{"us1":[[0,1.2,"timeout",null,null]],"de1":null}""";
            Assert.Equal(CheckOutcome.Inconclusive, CheckHostClient.InterpretResults(json));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("not json")]
        [InlineData("")]
        public void Empty_or_garbage_is_inconclusive(string json)
        {
            Assert.Equal(CheckOutcome.Inconclusive, CheckHostClient.InterpretResults(json));
        }
    }
}
