using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Tests.Revenue
{
    public class CurrencyLabelTests
    {
        [Fact]
        public void UsdtOnEthereum_HasItsOwnValueAndReadableLabel()
        {
            Assert.Equal(4, (int)Currency.UsdtEth);
            Assert.Equal("USDT (ETH)", Currency.UsdtEth.ToLabel());
        }

        [Theory]
        [InlineData(Currency.XMR, "XMR")]
        [InlineData(Currency.USD, "USD")]
        [InlineData(Currency.BTC, "BTC")]
        public void OtherCurrencies_KeepTheirCode(Currency currency, string expected)
        {
            Assert.Equal(expected, currency.ToLabel());
        }

        [Fact]
        public void ExistingStoredValues_AreUnchanged()
        {
            Assert.Equal(0, (int)Currency.Unknown);
            Assert.Equal(1, (int)Currency.XMR);
            Assert.Equal(2, (int)Currency.USD);
            Assert.Equal(3, (int)Currency.BTC);
        }
    }
}
