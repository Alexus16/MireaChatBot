using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MireaChatBot.ScheduleSender
{
    public class GroupChatRegistrator
    {
        private List<ChatClientProvider> _providers;
        private IBotClient _botClient;
    }

    public class ProviderArgs
    {
        public ProviderArgs(IChatClient oldClient, IChatClient newClient)
        {
            OldClient = oldClient;
            NewClient = newClient;
        }

        public IChatClient OldClient { get; }
        public IChatClient NewClient { get; }
    }

    public class ChatClientProvider
    {
        private IChatClient _adminChatClient;
        private IChatClient _groupChatClient;
        private IChatClient _supervisorChatClient;
        private Group _group;

        public event EventHandler<ProviderArgs> AdminChatUpdated;
        public event EventHandler<ProviderArgs> SupervisorChatUpdated;
        public event EventHandler<ProviderArgs> GroupChatUpdated;

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
            var args = new ProviderArgs(_adminChatClient, client);
            AdminChatUpdated?.Invoke(this, args);
            _adminChatClient = client;
        }

        public void SetSupervisorChat(IChatClient client)
        {
            var args = new ProviderArgs(_supervisorChatClient, client);
            SupervisorChatUpdated?.Invoke(this, args);
            _supervisorChatClient = client;
        }

        public void SetGroupChat(IChatClient client)
        {
            var args = new ProviderArgs(_groupChatClient, client);
            GroupChatUpdated?.Invoke(this, args);
            _groupChatClient = client;
        }
    }

    class PollSender
    {
        private readonly string _title = "Какие пары прогуливаешь {date}?";
        private GroupSchedule _schedule;
        private readonly ChatClientProvider _provider;
        private IVisitorDataContainer _visitorData;
        private PollSenderDayData _data;

        public PollSender(ChatClientProvider provider)
        {
            _provider = provider;
            _provider.GroupChatUpdated += groupUpdatedHandler;
            _provider.AdminChatUpdated += adminUpdatedHandler;
            _provider.SupervisorChatUpdated += supervisorUpdatedHandler;
        }

        public void SetVisitorData(IVisitorDataContainer visitorData)
        {
            _visitorData = visitorData;
        }

        public void SendReportToSupervisor()
        {
            _provider.SupervisorChatClient.SendMessage(new SendMessageArgs(createReport()));
        }

        public void SendReportToAdmin()
        {
            _provider.AdminChatClient.SendMessage(new SendMessageArgs(createReport()));
        }
        public void CloseDay()
        {
            SendReportToSupervisor();
            _provider.GroupChatClient.DeleteMessage(new DeleteMessageArgs("d"));
        }

        public void OpenDay()
        {
        }
        private SendPollArgs createPollArgsForNextDay()
        {
            DateTime tomorrowDate = DateTime.Now.AddDays(1);
            var activities = _schedule.GetDayEducationalActivities(tomorrowDate);
            string title = _title.Replace("{date}", tomorrowDate.ToString("dd.MM.yyyy"));
            List<string> options = new List<string>();
            foreach (var activity in activities)
            {
                options.Add($"[{activity.StartTime.ToString("HH:mm")}] {(activity as Activity).Name}");
            }
            var args = new SendPollArgs(title, options, false, true);
            return args;
        }

        private string createReport()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Статистика отсутствующих");
            builder.AppendLine();
            builder.AppendLine($"Дата: ");
            builder.AppendLine();

            return builder.ToString();
        }

        private void adminUpdatedHandler(object sender, ProviderArgs args)
        {
            args.OldClient.MessageReceived -= adminMessageReceivedHandler;
            args.NewClient.MessageReceived += adminMessageReceivedHandler;
        }
        private void supervisorUpdatedHandler(object sender, ProviderArgs args)
        {
            args.OldClient.MessageReceived -= supervisorMessageReceivedHandler;
            args.NewClient.MessageReceived += supervisorMessageReceivedHandler;
        }
        private void groupUpdatedHandler(object sender, ProviderArgs args)
        {
            args.OldClient.PollAnswerReceived -= groupPollAnswerReceivedHandler;
            args.NewClient.PollAnswerReceived += groupPollAnswerReceivedHandler;
        }
        private void adminMessageReceivedHandler(object sender, Message message)
        {
        }
        private void supervisorMessageReceivedHandler(object sender, Message message)
        {
        }
        private void groupPollAnswerReceivedHandler(object sender, PollAnswer answer)
        {

        }
    }

    interface IVisitorDataContainer
    {
        IVisitor GetVisitorById(string id);
    }

    class ChatVisitorDataCollector : IVisitorDataContainer, IDisposable
    {
        private readonly string _commandPattern = @"/имя (?<newName>.*)";
        private readonly Regex _botCommandRegex;
        private readonly IBotClient _client;
        private List<IVisitor> _visitors;

        public ChatVisitorDataCollector(IBotClient client)
        {
            if (client is null) throw new ArgumentNullException("Bot client can't be null", nameof(client));
            _client = client;
            _visitors = new List<IVisitor>();
            _botCommandRegex = new Regex(_commandPattern);
            subscribeOnBotClient();
        }

        private void subscribeOnBotClient()
        {
            _client.MessageReceived += clientMessageHandler;
        }

        private void unsubscribeFromBotClient()
        {
            _client.MessageReceived -= clientMessageHandler;
        }

        private void clientMessageHandler(object sender, Message message)
        {
            string messageText = message.Text;
            var commandMatch = _botCommandRegex.Match(messageText);
            if (!commandMatch.Success) return;
            var visitor = _visitors.Where(v => v.Id == message.From.Id).FirstOrDefault();
            if (visitor is null)
            {
                visitor = new StudentData(message.From.Id, "");
                _visitors.Add(visitor);
            }
            visitor.FullName = commandMatch.Groups["newName"].Value;
            _client.GetChat(message.Chat.Id).SendMessage(new SendMessageArgs("Имя установлено"));
        }

        public void Dispose()
        {
            unsubscribeFromBotClient();
        }

        public IVisitor GetVisitorById(string id)
        {
            var visitor = _visitors.Where(v => v.Id == id).FirstOrDefault();
            if (visitor is null)
            {
                visitor = new StudentData(id, "");
                _visitors.Add(visitor);
            }
            return visitor;
        }
    }

    class PollSenderDayData
    {
        public List<StudentData> Students { get; }
        public List<Activity> DayActivitySchedule { get; }
        public DateTime Date { get; }
    }

    class StudentData : IVisitor
    {
        private List<bool> _statuses;
        private string _fullname;
        private string _id;
        public StudentData(string id, string fullname)
        {
            _id = id;
            _fullname = fullname;
        }

        public void ResetStatuses(int newAmount)
        {
            _statuses = new List<bool>();
            for (int i = 0; i++ < newAmount; _statuses.Add(true)) ;
        }

        public void SetStatus(int index, bool value)
        {
            if (index >= _statuses.Count || index < 0) throw new IndexOutOfRangeException("Activity index out of range");
            _statuses[index] = value;
        }

        public IEnumerable<bool> Statuses => _statuses;

        public string FullName
        {
            get => _fullname;
            set => _fullname = value;
        }

        public string Id => _id;
    }

    interface IVisitor
    {
        IEnumerable<bool> Statuses { get; }
        string Id { get; }
        string FullName { get; set; }
        void ResetStatuses(int newAmount);
        void SetStatus(int index, bool value);
    }
}
