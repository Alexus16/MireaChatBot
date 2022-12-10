using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.ChatRegistation
{
    public class ChatClientProvider
    {
        private IChatClient _adminChatClient;
        private IChatClient _groupChatClient;
        private IChatClient _supervisorChatClient;
        private Group _group;

        public event EventHandler<ClientChangedArgs> AdminChatUpdated;
        public event EventHandler<ClientChangedArgs> SupervisorChatUpdated;
        public event EventHandler<ClientChangedArgs> GroupChatUpdated;

        public ChatClientProvider(Group group)
        {
            _group = group;
        }

        public Group Group => _group;
        public IChatClient AdminChatClient => _adminChatClient;
        public IChatClient SupervisorChatClient => _supervisorChatClient;
        public IChatClient GroupChatClient => _groupChatClient;

        public void SetAdminChat(IChatClient client)
        {
            var args = new ClientChangedArgs(_adminChatClient, client);
            AdminChatUpdated?.Invoke(this, args);
            _adminChatClient = client;
        }

        public void SetSupervisorChat(IChatClient client)
        {
            var args = new ClientChangedArgs(_supervisorChatClient, client);
            SupervisorChatUpdated?.Invoke(this, args);
            _supervisorChatClient = client;
        }

        public void SetGroupChat(IChatClient client)
        {
            var args = new ClientChangedArgs(_groupChatClient, client);
            GroupChatUpdated?.Invoke(this, args);
            _groupChatClient = client;
        }
    }
}
