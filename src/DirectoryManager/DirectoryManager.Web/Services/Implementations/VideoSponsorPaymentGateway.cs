using BtcPayServer.API.Implementations;
using BtcPayServer.API.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class VideoSponsorPaymentGateway : IVideoSponsorPaymentGateway
    {
        private readonly Lazy<IBtcPayServerService?> client;

        public VideoSponsorPaymentGateway(IConfiguration configuration)
        {
            this.client = new Lazy<IBtcPayServerService?>(() => Build(configuration));
        }

        public bool IsConfigured => this.client.Value is not null;

        public IBtcPayServerService? Client => this.client.Value;

        private static IBtcPayServerService? Build(IConfiguration configuration)
        {
            var videoStore = PaymentProcessorSettings.GetVideoSponsorStore(configuration);
            return videoStore is null ? null : new BtcPayServerService(videoStore);
        }
    }
}
