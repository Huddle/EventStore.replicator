using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Replicator.Shared.Logging;

namespace EventStore.Replicator.Esdb.Tcp {
    class Realtime {
        static readonly ILog Log = LogProvider.GetCurrentClassLogger();
        
        readonly IEventStoreConnection _connection;

        readonly StreamMetaCache _metaCache;

        bool _started;

        public Realtime(IEventStoreConnection connection, StreamMetaCache metaCache) {
            _connection = connection;
            _metaCache = metaCache;
        }

        public Task Start() {
            if (_started) return Task.CompletedTask;
            
            Log.Info("Starting realtime subscription for meta updates");
            _started = true;
            
            return _connection.SubscribeToAllAsync(
                false,
                (_, re) => HandleEvent(re),
                HandleDrop
            );
        }

        void HandleDrop(EventStoreSubscription subscription, SubscriptionDropReason reason, Exception exception) {
            if (reason == SubscriptionDropReason.UserInitiated) return;

            _started = false;
            Task.Run(Start);
        }

        Task HandleEvent(ResolvedEvent re) {
            if (IsSystemEvent())
                return Task.CompletedTask;

            if (IsMetadataUpdate()) {
                var stream = re.OriginalStreamId[2..];
                var meta   = StreamMetadata.FromJsonBytes(re.Event.Data);
                
                if (Log.IsDebugEnabled())
                    Log.Debug("Real-time meta update {Stream}: {Meta}", stream, meta);
                
                _metaCache.UpdateStreamMeta(stream, meta, re.OriginalEventNumber, re.OriginalEvent.Created);
            }
            else {
                _metaCache.UpdateStreamLastEventNumber(re.OriginalStreamId, re.OriginalEventNumber);
            }
            return Task.CompletedTask;

            bool IsSystemEvent()
                => re.Event.EventType.StartsWith('$') && re.Event.EventType != Predefined.MetadataEventType;

            bool IsMetadataUpdate() => re.Event.EventType == Predefined.MetadataEventType;
        }
    }
}