using Microsoft.AspNetCore.SignalR;
using Orleans;
using Orleans.Runtime;

namespace UploadSubscriber
{
    public interface ISelfieServerDirectory : IGrainWithIntegerKey
    {
        Task ReceiveSelfie(Selfie selfie);
        Task<int> GetActiveClientCount();
        Task ClientConnected(string connectionId);
        Task ClientDisconnected(string connectionId);
        Task ServerOnline(ISelfieServer server);
        Task ServerOffline(ISelfieServer server);
        Task<List<Selfie>> GetLastFiveSelfies();
    }

    public interface ISelfieServer : IGrainObserver
    {
        Task SelfiesUpdated(List<Selfie> selfies);
        Task ClientsUpdated(int clientCount);
    }

    public record SelfieUserInfo(string ConnectionId);

    public interface ISelfieUser : IGrainWithStringKey
    {
        Task<SelfieUserInfo> Get();
        Task SignIn();
        Task SignOut();
    }

    public class SelfieUser : Grain, ISelfieUser
    {
        public Task<SelfieUserInfo> Get()
        {
            return Task.FromResult(new SelfieUserInfo(this.GetGrainIdentity().PrimaryKeyString));
        }

        public Task SignIn()
        {
            return Task.CompletedTask;
        }

        public Task SignOut()
        {
            // usually not a good idea to do this, mainly using to show deletion on disconnection.
            base.DeactivateOnIdle();
            return Task.CompletedTask;
        }
    }

    public class SelfieServerDirectory : Grain, ISelfieServerDirectory
    {
        private IPersistentState<List<Selfie>> _selfies;
        private IPersistentState<int> _clientCount;
        private ILogger<SelfieServerDirectory> _logger;

        public SelfieServerDirectory(
            [PersistentState("Selfies")] IPersistentState<List<Selfie>> selfies, 
            [PersistentState("ClientCount")] IPersistentState<int> clientCount, 
            ILogger<SelfieServerDirectory> logger)
        {
            _selfies = selfies;
            _clientCount = clientCount;
            _logger = logger;
        }

        private HashSet<ISelfieServer> Servers { get; set; } = new();

        public async Task ClientConnected(string connectionId)
        {
            _clientCount.State += 1;

            // sign in
            var connectionGrain = GrainFactory.GetGrain<ISelfieUser>(connectionId);
            await connectionGrain.SignIn();

            // notify all the servers someone joined
            foreach (var observer in Servers)
            {
                await observer.ClientsUpdated(_clientCount.State);
            }
        }

        public async Task ClientDisconnected(string connectionId)
        {
            _clientCount.State -= 1;

            // sign out
            var connectionGrain = GrainFactory.GetGrain<ISelfieUser>(connectionId);
            await connectionGrain.SignOut();

            // notify all the servers someone left
            foreach (var observer in Servers)
            {
                await observer.ClientsUpdated(_clientCount.State);
            }
        }

        public Task<int> GetActiveClientCount()
            => Task.FromResult(_clientCount.State);

        public async Task ReceiveSelfie(Selfie selfie)
        {
            await _selfies.ReadStateAsync();

            if(!_selfies.State.Any(x => x.Url == selfie.Url))
            {
                _selfies.State.Add(selfie);

                // get the last five selfies taken to show to folks on-screen
                var lastFive = await GetLastFiveSelfies();
                foreach (var observer in Servers)
                {
                    await observer.SelfiesUpdated(lastFive);
                }
            }

            await _selfies.WriteStateAsync();
        }

        public Task ServerOnline(ISelfieServer server)
        {
            Servers.Add(server);
            return Task.CompletedTask;
        }

        public Task ServerOffline(ISelfieServer server)
        {
            Servers.Remove(server);
            return Task.CompletedTask;
        }

        public async Task<List<Selfie>> GetLastFiveSelfies()
        {
            return _selfies.State.TakeLast(5).ToList();
        } 
    }

    public class SelfieServer : ISelfieServer
    {
        IHubContext<SelfieHub, ISelfieServer> _hubContext;
        ILogger<SelfieServer> _logger;

        public SelfieServer(IHubContext<SelfieHub, ISelfieServer> hubContext, 
            ILogger<SelfieServer> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task ClientsUpdated(int clientCount)
            => await _hubContext.Clients.All.ClientsUpdated(clientCount);

        public async Task SelfiesUpdated(List<Selfie> selfies)
            => await _hubContext.Clients.All.SelfiesUpdated(selfies);
    }

    public class SelfieServerWorker : IHostedService
    {
        public IGrainFactory GrainFactory { get; set; }
        public ISelfieServer SelfieServer { get; set; }
        public ISelfieServerDirectory SelfieServerDirectory { get; set; }

        public SelfieServerWorker(IGrainFactory grainFactory,
            ISelfieServer selfieServer)
        {
            GrainFactory = grainFactory;
            SelfieServer = selfieServer;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            SelfieServerDirectory = GrainFactory.GetGrain<ISelfieServerDirectory>(0);
            var reference = await GrainFactory.CreateObjectReference<ISelfieServer>(SelfieServer);
            await SelfieServerDirectory.ServerOnline(reference);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var reference = await GrainFactory.CreateObjectReference<ISelfieServer>(SelfieServer);
            await SelfieServerDirectory.ServerOffline(reference);
        }
    }
}
