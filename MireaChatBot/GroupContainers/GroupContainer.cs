using MireaChatBot.BotHandlers;
using MireaChatBot.ChatHandlers;
using MireaChatBot.ChatRegistation;
using MireaChatBot.DataContainers;
using MireaChatBot.ScheduleParsers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MireaChatBot.GroupContainers
{
    public interface IGroupContainer
    {
        IGroupContext Context { get; }
        T GetChatHandler<T>() where T : class, ISpecialChatHandler;
        void AddHandler<T>(T handler) where T : class, ISpecialChatHandler;
    }
    public interface IGroupContext
    {
        ChatClientProvider Provider { get; }
        T GetDataContainer<T>() where T : class, IDataContainer;
        void AddDataContainer<T>(T container) where T : class, IDataContainer;
    }
    public interface IGroupContainerBuilder
    {
        IChatClient AdminChat { get; }
        IBotClient Client { get; }
        void AddHandlerFactory<T>() where T : class, ISpecialChatHandlerFactory, new();
        void AddDataContainer<T>(T containerInstance) where T : class, IDataContainer;
        T GetDataContainer<T>() where T : class, IDataContainer;
        IEnumerable<IGroupContainer> Groups { get; }
        IGroupContainer GetGroup(Group groupInfo);
        IGroupContainer GetGroup(string groupName);
        IGroupContainer RegisterGroup(Group groupInfo);
    }


    public class GroupContext : IGroupContext
    {
        private List<IDataContainer> _dataContainers;
        private ChatClientProvider _provider;

        public event EventHandler SendSupervisorDefaultCommandsRequested;

        public GroupContext(ChatClientProvider provider)
        {
            _provider = provider;
            _dataContainers = new List<IDataContainer>();
        }

        public ChatClientProvider Provider => _provider;
        public T GetDataContainer<T>() where T : class, IDataContainer
        {
            return _dataContainers.Where(dc => dc is T).FirstOrDefault() as T;
        }
        public void AddDataContainer<T>(T container) where T : class, IDataContainer
        {
            _dataContainers.Add(container);
        }

        public void SendSupervisorCommands()
        {
            SendSupervisorDefaultCommandsRequested?.Invoke(this, new EventArgs());
        }
    }

    public class GroupContainer : IGroupContainer
    {
        private GroupContext _context;
        private List<ISpecialChatHandler> _chatHandlers;
        public GroupContainer(GroupContext context)
        {
            _context = context;
            _chatHandlers = new List<ISpecialChatHandler>();
        }

        public IGroupContext Context => _context;

        public T GetChatHandler<T>() where T : class, ISpecialChatHandler
        {
            return _chatHandlers.Where(h => h is T).FirstOrDefault() as T;
        }

        public void AddHandler<T>(T handler) where T : class, ISpecialChatHandler
        {
            _chatHandlers.Add(handler);
        }
    }

    public class GroupContainerBuilder : IGroupContainerBuilder
    {
        private readonly Random _random = new Random();
        private readonly ChatProviderRegistrator _registrator;
        private readonly IBotClient _client;
        private List<GroupContainer> _containers;
        private List<ISpecialChatHandlerFactory> _handlerFactories;
        private List<ICommonChatHandler> _botHandlers;
        private List<IDataContainer> _dataContainers;

        public GroupContainerBuilder(IBotClient client)
        {
            _client = client;
            _registrator = new ChatProviderRegistrator(_client, () => HashCalculator.GetHashString(_random.Next().ToString()));
            _containers = new List<GroupContainer>();
            _handlerFactories = new List<ISpecialChatHandlerFactory>();
            _botHandlers = new List<ICommonChatHandler>();
            _dataContainers = new List<IDataContainer>();
        }

        public IBotClient Client => _client;

        public IEnumerable<IGroupContainer> Groups => _containers;
        public IEnumerable<IDataContainer> DataContainers => _dataContainers;
        public IEnumerable<ICommonChatHandler> Handlers => _botHandlers;
        public IEnumerable<ISpecialChatHandlerFactory> HandlerFactories => _handlerFactories;

        public IChatClient AdminChat => _registrator.AdminClient;
        public T GetDataContainer<T>() where T : class, IDataContainer
        {
            return _dataContainers.Where(c => c is T).FirstOrDefault() as T;
        }

        public IGroupContainer GetGroup(Group groupInfo)
        {
            return _containers.Where(c => c.Context.Provider.Group.Name == groupInfo.Name).FirstOrDefault();
        }

        public IGroupContainer GetGroup(string groupName)
        {
            return _containers.Where(c => c.Context.Provider.Group.Name == groupName).FirstOrDefault();

        }

        public IGroupContainer RegisterGroup(Group groupInfo)
        {
            var context = createContextForGroup(groupInfo);
            var groupContainer = createGroupContainer(context);
            _containers.Add(groupContainer);
            return groupContainer;
        }

        private GroupContext createContextForGroup(Group groupInfo)
        {
            var provider = createProviderForGroup(groupInfo);
            var context = new GroupContext(provider);
            foreach(var container in _dataContainers)
            {
                context.AddDataContainer(container);
            }
            return context;
        }
        private ChatClientProvider createProviderForGroup(Group groupInfo)
        {
            var provider = _registrator.CreateNewProvider(groupInfo);
            return provider;
        }

        private GroupContainer createGroupContainer(GroupContext context)
        {
            var groupContainer = new GroupContainer(context);
            foreach(var factory in _handlerFactories)
            {
                groupContainer.AddHandler(factory.CreateHandler(context));
            }
            return groupContainer;
        }

        public void AddDataContainer<T>(T containerInstance) where T : class, IDataContainer
        {
            if (_dataContainers.Any(c => c is T)) return;
            containerInstance.SetBuilder(this);
            _dataContainers.Add(containerInstance);
        }

        public void AddHandlerFactory<T>() where T : class, ISpecialChatHandlerFactory, new()
        {
            if (_handlerFactories.Any(hf => hf is T)) return;
            T newFactory = new T();
            newFactory.SetBuilder(this);
            _handlerFactories.Add(newFactory);
        }

        public void AddHandler<T>() where T : class, ICommonChatHandler, new()
        {
            if (_botHandlers.Any(h => h is T)) return;
            T newHandler = new T();
            newHandler.SetBuilder(this);
            _botHandlers.Add(newHandler);
        }
    }
}
