using Microsoft.AspNetCore.SignalR;
using Orleans;

namespace UploadSubscriber
{
    public class SelfieHub : Hub<ISelfieServer>
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<SelfieHub> _logger;
        private readonly ISelfieServer _selfieServer;

        public SelfieHub(IGrainFactory grainFactory,
            ILogger<SelfieHub> logger,
            ISelfieServer selfieServer)
        {
            _grainFactory = grainFactory;
            _logger = logger;
            _selfieServer = selfieServer;
        }

        public override async Task OnConnectedAsync()
        {
            var selfieServerDirectory = _grainFactory.GetGrain<ISelfieServerDirectory>(0);
            await selfieServerDirectory.ClientConnected(Context.ConnectionId);
            await UpdateClientCount(selfieServerDirectory);

            var lastFewSelfies = await selfieServerDirectory.GetLastFiveSelfies();
            await Clients.Caller.SelfiesUpdated(lastFewSelfies);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var selfieServerDirectory = _grainFactory.GetGrain<ISelfieServerDirectory>(0);
            await selfieServerDirectory.ClientDisconnected(Context.ConnectionId);
            await UpdateClientCount(selfieServerDirectory);
        }

        private async Task UpdateClientCount(ISelfieServerDirectory directory)
        {
            var currentClientCount = await directory.GetActiveClientCount();
            await Clients.All.ClientsUpdated(currentClientCount);
        }
    }
}
