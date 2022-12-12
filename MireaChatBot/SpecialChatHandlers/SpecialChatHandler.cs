using MireaChatBot.ChatRegistation;
using MireaChatBot.DataContainers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;

namespace MireaChatBot.ChatHandlers
{
    public interface ICommandRouter
    {
        void OnMessageReceived(Message message);
        event EventHandler<CommandReceivedArgs> CommandReceived;
    }
    public interface ISpecialChatHandler
    {
        IGroupContext GroupContext { get; }
    }

    public interface ISpecialChatHandlerFactory
    {
        IGroupContainerBuilder Builder { get; }
        void SetBuilder(IGroupContainerBuilder builder);
        ISpecialChatHandler CreateHandler(IGroupContext context);
    }

    public class CommandReceivedArgs
    {
        private string _command;
        private Message _message;

        public CommandReceivedArgs(string command, Message message)
        {
            _command = command;
            _message = message;
        }

        public string Command => _command;
        public Message Message => _message;
    }
}