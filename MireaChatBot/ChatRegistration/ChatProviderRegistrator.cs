using DocumentFormat.OpenXml.Bibliography;
using MireaChatBot.ScheduleParsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MireaChatBot.ChatRegistation
{
    public class ChatProviderRegistrator
    {
        private List<ChatClientProviderRecord> _providersRecords;
        private IBotClient _botClient;
        private IChatClient _adminClient;
        private string _adminToken;
        private Func<string> _createTokenFunc;
        
        public ChatProviderRegistrator(IBotClient botClient, Func<string> createTokenFunc)
        {
            if(createTokenFunc is null)
            {
                throw new ArgumentNullException("Generator function can't be NULL", nameof(createTokenFunc));
            }
            _createTokenFunc = createTokenFunc;
            _adminToken = _createTokenFunc();
            Console.WriteLine(_adminToken);
            _providersRecords = new List<ChatClientProviderRecord>();
            _botClient = botClient;
            _botClient.MessageReceived += messageReceiveHandler;
        }
        public IChatClient AdminClient => _adminClient;
        public ChatClientProvider CreateNewProvider(Group groupInfo)
        {
            if(_adminClient is null)
            {
                Console.WriteLine("No admin specified");
                throw new InvalidOperationException("No admin specified");
            }
            string supervisorToken = _createTokenFunc();
            string groupToken = _createTokenFunc();
            _adminClient.SendMessage(new SendMessageArgs(getRecordInfo(groupInfo.Name, supervisorToken, groupToken)));
            var provider = new ChatClientProvider(groupInfo);
            string supervisorTokenHash = getHashString(supervisorToken);
            string groupTokenHash = getHashString(groupToken);
            var record = new ChatClientProviderRecord(provider, groupTokenHash, supervisorTokenHash);
            provider.SetAdminChat(_adminClient);
            _providersRecords.Add(record);
            return provider;
        }

        private void messageReceiveHandler(object sender, Message message)
        {
            string messageText = message.Text;
            if(messageText == _adminToken)
            {
                _adminClient = _botClient.GetChat(message.Chat.Id);
                _adminClient.SendMessage(new SendMessageArgs("Admin registered"));
                foreach(var record in _providersRecords)
                {
                    record.Provider.SetAdminChat(_adminClient);
                }
                return;
            }
            foreach(var record in _providersRecords)
            {
                if(getHashString(messageText) == record.GroupTokenHash)
                {
                    var client = _botClient.GetChat(message.Chat.Id);
                    client.DeleteMessage(new DeleteMessageArgs(message.Chat.Id));
                    client.SendMessage(new SendMessageArgs($"Group chat {record.Provider.Group.Name} registered"));
                    record.Provider.SetGroupChat(client);
                    return;
                }
                else if (getHashString(messageText) == record.SupervisorTokenHash)
                {
                    var client = _botClient.GetChat(message.Chat.Id);
                    client.DeleteMessage(new DeleteMessageArgs(message.Chat.Id));
                    client.SendMessage(new SendMessageArgs($"Supervisor chat {record.Provider.Group.Name} registered"));
                    record.Provider.SetSupervisorChat(client);
                    return;
                }
            }
            
        }

        private string getRecordInfo(string groupName, string svToken, string gToken)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"NEW RECORD CREATED");
            builder.AppendLine($"Name: {groupName}");
            builder.AppendLine($"AUTH TOKENS");
            builder.AppendLine($"Supervisor token: {svToken}");
            builder.AppendLine($"Group token: {gToken}");
            return builder.ToString();
        }

        private string getHashString(string str)
        {
            return HashCalculator.GetHashString(str);
        }
    }

    public class ChatClientProviderRecord
    {
        private ChatClientProvider _provider;
        private string _groupTokenHash;
        private string _supervisorTokenHash;

        public ChatClientProviderRecord(ChatClientProvider provider, string groupToken, string supervisorToken)
        {
            _provider = provider;
            _groupTokenHash = groupToken;
            _supervisorTokenHash = supervisorToken;
        }

        public ChatClientProvider Provider => _provider;
        public string GroupTokenHash => _groupTokenHash;
        public string SupervisorTokenHash => _supervisorTokenHash;
    }

    public class ClientChangedArgs
    {
        public ClientChangedArgs(IChatClient oldClient, IChatClient newClient)
        {
            OldClient = oldClient;
            NewClient = newClient;
        }
        public IChatClient OldClient { get; }
        public IChatClient NewClient { get; }
    }
}
