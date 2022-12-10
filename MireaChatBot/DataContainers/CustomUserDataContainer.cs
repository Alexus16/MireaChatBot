using MireaChatBot.ChatHandlers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MireaChatBot.DataContainers
{
    interface IVisitorDataContainer
    {
        CustomUser GetVisitorById(string id);
    }
    public class CustomUser : User
    {
        public CustomUser(string id, string username) : base(id, username) { }
        public string FullName { get; set; }
    }
    class CustomUserDataContainer : IDataContainer, IDisposable
    {
        private readonly string[] _containerCommands = new string[] { "имя" };
        private IGroupContainerBuilder _builder;
        private readonly string _commandPattern = @"/имя (?<newName>.*)";
        private readonly Regex _commandRegex;
        private List<CustomUser> _visitors;

        public IGroupContainerBuilder Builder => _builder;

        public CustomUserDataContainer()
        {
            _visitors = new List<CustomUser>();
            _commandRegex = new Regex(_commandPattern);
        }

        private void subscribeOnBotClient()
        {
            _builder.Client.MessageReceived += clientMessageHandler;
        }

        private void unsubscribeFromBotClient()
        {
            _builder.Client.MessageReceived -= clientMessageHandler;
        }

        private void clientMessageHandler(object sender, Message message)
        {
            string messageText = message.Text;
            var commandMatch = _commandRegex.Match(messageText);
            if (!commandMatch.Success) return;
            var visitor = _visitors.Where(v => v.Id == message.From.Id).FirstOrDefault();
            if (visitor is null)
            {
                visitor = new CustomUser(message.From.Id, "");
                _visitors.Add(visitor);
            }
            visitor.FullName = commandMatch.Groups["newName"].Value;
            _builder.Client.GetChat(message.Chat.Id).SendMessage(new SendMessageArgs("Имя установлено"));
        }

        public void Dispose()
        {
            unsubscribeFromBotClient();
        }

        public CustomUser GetVisitorById(string id)
        {
            var visitor = _visitors.Where(v => v.Id == id).FirstOrDefault();
            if (visitor is null)
            {
                visitor = new CustomUser(id, "");
                _visitors.Add(visitor);
            }
            return visitor;
        }

        public void SetBuilder(IGroupContainerBuilder builder)
        {
            _builder = builder;
            subscribeOnBotClient();
        }

        public IEnumerable<T> GetDataCollection<T>() where T : class
        {
            if (typeof(T) != typeof(CustomUser)) return null;
            List<T> visitorsT = new List<T>();
            for (int i = 0; i < _visitors.Count; i++)
            {
                visitorsT.Add(_visitors[i] as T);
            }
            return visitorsT;
        }

        public IEnumerable<string> GetDataContainerCommands()
        {
            return _containerCommands;
        }
    }
}
