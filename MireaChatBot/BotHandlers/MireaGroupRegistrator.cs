using MireaChatBot.GroupContainers;
using System.Text.RegularExpressions;

namespace MireaChatBot.BotHandlers
{
    public class MireaGroupRegistratorHandler : ICommonChatHandler
    {
        private readonly MireaGroupFactory _groupFactory = new MireaGroupFactory();
        private readonly string _commandPattern = @"/зарегистрировать (?<groupName>.*)";
        private IGroupContainerBuilder _builder;
        public IGroupContainerBuilder Builder => _builder;
        public MireaGroupRegistratorHandler() { }
        public void SetBuilder(IGroupContainerBuilder builder)
        {
            if (!(_builder is null)) unsubscribeFromBuilder();
            _builder = builder;
            subscribeOnBuilder();
        }

        private void subscribeOnBuilder()
        {
            _builder.Client.MessageReceived += botMessageReceived;
        }
        private void unsubscribeFromBuilder()
        {
            _builder.Client.MessageReceived -= botMessageReceived;
        }

        private void botMessageReceived(object sender, Message message)
        {
            if (message.Chat.Id != (_builder.AdminChat?.ChatId ?? "")) return;
            string messageText = message.Text;
            Regex commandRegex = new Regex(_commandPattern);
            if (!commandRegex.IsMatch(messageText)) return;
            Match commandMatch = commandRegex.Match(messageText);
            string groupName = commandMatch.Groups["groupName"].Value;
            Builder.RegisterGroup(_groupFactory.Create(groupName));
        }
    }
}