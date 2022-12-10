using MireaChatBot.ChatRegistation;
using MireaChatBot.DataContainers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace MireaChatBot.ChatHandlers
{
    public class AttachmentHandlerFactory : ISpecialChatHandlerFactory
    {
        private IGroupContainerBuilder _builder;
        public IGroupContainerBuilder Builder => _builder;

        public ISpecialChatHandler CreateHandler(IGroupContext context)
        {
            return new AttachmentHandler(context);
        }

        public void SetBuilder(IGroupContainerBuilder builder)
        {
            _builder = builder;
        }
    }
    public class AttachmentHandler : ISpecialChatHandler
    {
        private IEnumerable<string> _supervisorCommanвs = new string[] {"/приложить", "/закрыть"};
        private List<AttachmentRecord> _records;
        private Thread _autosendThread;
        private CancellationTokenSource _autosendThreadControl;
        private AttachmentHandlerState _currentState;
        private AttachmentHandlerData _currentData;
        private IGroupContext _context;
        private DateTime _autosendTime;
        private DateTime _todayAutosendTime;
        private bool _isDayClosed;
        public AttachmentHandler(IGroupContext context)
        {
            _context = context;
            _currentData = new AttachmentHandlerData();
            _currentState = new DefaultState(this);
            _records = new List<AttachmentRecord>();
            subsribeOnProvider();
            _isDayClosed = false;
            _autosendTime = _context.GetDataContainer<KeyTimeDataContainer>().GetDataCollection<KeyTime>().
            Where(kt => kt.Key == "poll-send").
            FirstOrDefault()?.Time ?? new DateTime(1, 1, 1, 19, 0, 0);
            _todayAutosendTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, _autosendTime.Hour, _autosendTime.Minute, _autosendTime.Second);
            _autosendThreadControl = new CancellationTokenSource();
            _autosendThread = new Thread(() => { });
            _autosendThread.Start();
        }

        public AttachmentHandlerData CurrentData => _currentData;
        public List<AttachmentRecord> Records => _records;
        public IGroupContext GroupContext => _context;

        private void autosendThreadTick()
        {
            while(!_autosendThreadControl.Token.IsCancellationRequested)
            {
                if(Math.Abs((DateTime.Now -  _todayAutosendTime).TotalMinutes) < 1 && !_isDayClosed)
                {
                    _isDayClosed = true;
                    SendAttachmentsForNextDay();
                    
                }
                else if (Math.Abs((DateTime.Now - _todayAutosendTime.AddMinutes(1)).TotalMinutes) < 1 && _isDayClosed)
                {
                    DateTime tomorrowDateTime = DateTime.Now.AddDays(1);
                    _todayAutosendTime = new DateTime(tomorrowDateTime.Year, tomorrowDateTime.Month, tomorrowDateTime.Day, _autosendTime.Hour, _autosendTime.Minute, _autosendTime.Second);
                }
            Thread.Sleep(60000);
            }
        }

        public void SendAttachmentsForNextDay()
        {
            if (_context.Provider.GroupChatClient is null) return;
            DateTime tomorrowDate = DateTime.Now.AddDays(1);
            var messages = createMessagesForDate(tomorrowDate);
            if (messages.Count() == 0) return;
            foreach (var message in messages)
            {
                _context.Provider.GroupChatClient.SendMessage(message);
            }
        }

        private IEnumerable<SendFileArgs> createMessagesForDate(DateTime date)
        {
            List<SendFileArgs> messages = new List<SendFileArgs>();
            var matchedRecords = _records.Where(r => r.Date.ToString("yyyy-MM-dd") == date.ToString("yyyy-MM-dd"));
            foreach(var record in matchedRecords)
            {
                string messageText = $"{record.Activity.Name}\n{record.Comment}";
                SendFileArgs message = new SendFileArgs(messageText, record.AttachmentInfo?.FullName ?? "");
                messages.Add(message);
            }
            return messages;
        }

        private void subsribeOnProvider()
        {
            if (!(_context.Provider.SupervisorChatClient is null)) _context.Provider.SupervisorChatClient.MessageReceived += supervisorMessageReceivedHandler;
            _context.Provider.SupervisorChatUpdated += supervisorChangedHandler;
        }

        private void unsubsribeFromProvider()
        {
            if (!(_context.Provider.SupervisorChatClient is null)) _context.Provider.SupervisorChatClient.MessageReceived -= supervisorMessageReceivedHandler;
            _context.Provider.SupervisorChatUpdated -= supervisorChangedHandler;
        }

        private void supervisorMessageReceivedHandler(object sender, Message message)
        {
            _currentState = _currentState.NextState(message);
        }

        private void supervisorChangedHandler(object sender, ClientChangedArgs args)
        {
            if (!(args.OldClient is null)) args.OldClient.MessageReceived -= supervisorMessageReceivedHandler;
            if (!(args.NewClient is null)) args.NewClient.MessageReceived += supervisorMessageReceivedHandler;
        }
    }

    public abstract class AttachmentHandlerState
    {
        private AttachmentHandler _handler;
        public AttachmentHandlerState(AttachmentHandler handler)
        {
            _handler = handler;
            OnGotControl();
        }
        public AttachmentHandler Handler => _handler;
        public IGroupContext GroupContext => _handler.GroupContext;
        public IChatClient SupervisorChat => GroupContext.Provider.SupervisorChatClient;
        private GroupSchedule groupSchedule => GroupContext.GetDataContainer<ScheduleDataContainer>().GetDataCollection<GroupSchedule>()
            .Where(gs => gs.Group.Name == GroupContext.Provider.Group.Name)
            .FirstOrDefault();
        public abstract AttachmentHandlerState NextState(Message message);
        public abstract void OnGotControl();
        public bool checkOnExitRequest(string messageText)
        {
            Regex exitRegex = new Regex(@"/выход");
            return exitRegex.IsMatch(messageText);
        }
        public IEnumerable<Activity> GetDayActivities(DateTime date)
        {
            return groupSchedule.GetDayEducationalActivities(date);
        }
    }

    public sealed class DefaultState : AttachmentHandlerState
    {
        private readonly Regex _commandRegex = new Regex(@"/(?<command>.*)");

        public DefaultState(AttachmentHandler handler) : base(handler) { }

        public override void OnGotControl()
        {
            GroupContext.SendSupervisorCommands();
        }

        public override AttachmentHandlerState NextState(Message message)
        {
            string messageText = message.Text;
            var commandMatch = _commandRegex.Match(messageText);
            if (!commandMatch.Success) return this;
            switch (commandMatch.Groups["command"].Value)
            {
                case "закрыть":
                    Handler.SendAttachmentsForNextDay();
                    return this;
                case "приложить":
                    return new WaitDateState(Handler);
                case "удалить":
                    return this;
                default:
                    return this;
            }
        }


    }
    public sealed class WaitDateState : AttachmentHandlerState
    {
        private readonly Regex _dateRegex = new Regex(@"(?<day>\d\d)\.(?<month>\d\d)(\.?)(?<year>(\d\d\d\d)?)");
        public WaitDateState(AttachmentHandler handler) : base(handler) { }

        public override void OnGotControl()
        {
            SupervisorChat.SendMessage(new SendMessageArgs("Укажите дату"));
        }
        public override AttachmentHandlerState NextState(Message message)
        {
            string messageText = message.Text;
            if (checkOnExitRequest(messageText)) return new DefaultState(Handler);
            Match dateMatch = _dateRegex.Match(messageText);
            if (!dateMatch.Success)
            {
                GroupContext.Provider.SupervisorChatClient.SendMessage(new SendMessageArgs("Некорректная дата"));
                return this;
            }
            Handler.CurrentData.RequestedDate = DateTime.Parse(messageText);
            return new WaitActivityState(Handler);
        }
    }
    public sealed class WaitActivityState : AttachmentHandlerState
    {
        private IEnumerable<Activity> _requestedDateActivities;
        public WaitActivityState(AttachmentHandler handler) : base(handler) { }
        private ReplyMarkup _activitiesMarkup => ReplyMarkup.Create(_requestedDateActivities.Select<Activity, string>(a => $"[{a.StartTime.ToString("HH:mm")}] {a.Name}"));
        public override void OnGotControl()
        {
            _requestedDateActivities = GetDayActivities(Handler.CurrentData.RequestedDate);
            SupervisorChat.SendMessage(new SendMessageArgs("Укажите активность для приложения", _activitiesMarkup));
        }
        public override AttachmentHandlerState NextState(Message message)
        {
            string messageText = message.Text;
            if (checkOnExitRequest(messageText)) return new DefaultState(Handler);
            var selectedActivity = _requestedDateActivities.Where(a => messageText == $"[{a.StartTime.ToString("HH:mm")}] {a.Name}").FirstOrDefault();
            if(selectedActivity is null)
            {
                SupervisorChat.SendMessage(new SendMessageArgs("Указанная активность не найдена", _activitiesMarkup));
                return this;
            }
            Handler.CurrentData.RequestedActivity = selectedActivity;
            return new WaitTextState(Handler);
        }

    }

    public sealed class WaitTextState : AttachmentHandlerState
    {
        public WaitTextState(AttachmentHandler handler) : base(handler) { }
        public override void OnGotControl()
        {
            SupervisorChat.SendMessage(new SendMessageArgs("Добавьте комментарий к активностм"));
        }
        public override AttachmentHandlerState NextState(Message message)
        {
            string messageText = message.Text;
            if (checkOnExitRequest(messageText)) return new DefaultState(Handler);
            Handler.CurrentData.Comment = messageText;
            return new WaitAttachmentState(Handler);
        }
    }

    public sealed class WaitAttachmentState : AttachmentHandlerState
    {
        private IEnumerable<string> noAttachmentReplyMarkup = new string[] { "Без приложения" };
        public WaitAttachmentState(AttachmentHandler handler) : base(handler) { }
        public override void OnGotControl()
        {
            SupervisorChat.SendMessage(new SendMessageArgs("Добавьте при необходимости приложения к активности", ReplyMarkup.Create(noAttachmentReplyMarkup)));
        }
        public override AttachmentHandlerState NextState(Message message)
        {
            string messageText = message.Text;
            if(checkOnExitRequest(messageText)) return new DefaultState(Handler);
            if (noAttachmentReplyMarkup.Contains(messageText)) return new SaveAttachmentState(Handler);
            if (message.Document is null) return this;
            Handler.CurrentData.AttachmentFile = SupervisorChat.GetFile(message.Document);
            return new SaveAttachmentState(Handler);
        }
    }

    public sealed class SaveAttachmentState : AttachmentHandlerState
    {
        public SaveAttachmentState(AttachmentHandler handler) : base(handler) { }

        public override AttachmentHandlerState NextState(Message message)
        {
            return new DefaultState(Handler);
        }

        public override void OnGotControl()
        {
            var newRecord = new AttachmentRecord(Handler.CurrentData.RequestedActivity, Handler.CurrentData.AttachmentFile, Handler.CurrentData.Comment, Handler.CurrentData.RequestedDate);
            Handler.Records.Add(newRecord);
            SupervisorChat.SendMessage(new SendMessageArgs("Сохранено"));
        }
    }

    public class AttachmentHandlerData
    {
        public DateTime RequestedDate { get; set; }
        public Activity RequestedActivity { get; set; }
        public string Comment { get; set; }
        public FileInfo AttachmentFile { get; set; }
    }

    public class AttachmentRecord
    {
        public AttachmentRecord(Activity activity, FileInfo attachmentInfo, string comment, DateTime date)
        {
            Activity = activity;
            AttachmentInfo = attachmentInfo;
            Comment = comment;
            Date = date;
        }
        public DateTime Date { get; }
        public Activity Activity { get; }
        public FileInfo AttachmentInfo { get; }
        public string Comment { get; }
    }
}
