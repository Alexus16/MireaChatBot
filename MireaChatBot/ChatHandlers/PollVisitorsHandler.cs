using MireaChatBot.ChatRegistation;
using MireaChatBot.DataContainers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MireaChatBot.ChatHandlers
{
    class PollVisitHandlerDayData
    {
        public string PollId { get; set; }
        public string PollMessageId { get; set; }
        public List<StudentVisitData> Students { get; set; }
        public List<Activity> DayActivitySchedule { get; set; }
        public DateTime Date { get; set; }
    }

    class PollVisitHandlerFactory : ISpecialChatHandlerFactory
    {
        private IGroupContainerBuilder _builder;
        public IGroupContainerBuilder Builder => _builder;

        public ISpecialChatHandler CreateHandler(IGroupContext context)
        {
            return new PollVisitHandler(context);
        }

        public void SetBuilder(IGroupContainerBuilder builder)
        {
            _builder = builder;
        }
    }

    class PollVisitHandler : ISpecialChatHandler
    {
        private IEnumerable<string> _allCommands = new string[] { "стат", "закрыть" };
        private Mutex _mutex = new Mutex();
        private Thread _autosendThread;
        private bool _isNextDayOpened;
        private readonly string _title = "Какие пары прогуливаешь {date}?";
        private IGroupContext _context;
        private PollVisitHandlerDayData _currentData;
        private readonly Regex _botCommandRegex = new Regex(@"/(?<command>\w\w*)");
        private DateTime _autosendTime;
        private DateTime _todayAutosendTime;
        private CancellationTokenSource _autosendControl;

        public IGroupContext GroupContext => _context;
        private IEnumerable<CustomUser> _customUsers => GroupContext.GetDataContainer<CustomUserDataContainer>().GetDataCollection<CustomUser>();
        private IEnumerable<GroupSchedule> _allSchedules => GroupContext.GetDataContainer<ScheduleDataContainer>().GetDataCollection<GroupSchedule>();
        private GroupSchedule _groupSchedule => _allSchedules.Where(gs => gs.Group.Name == _context.Provider.Group.Name).FirstOrDefault();

        public PollVisitHandler(IGroupContext context)
        {
            _context = context;
            _currentData = new PollVisitHandlerDayData();
            _isNextDayOpened = false;
            _autosendControl = new CancellationTokenSource();
            _autosendThread = new Thread(autosendThreadTick);
            _autosendThread.Start();
            _autosendTime = _context.GetDataContainer<KeyTimeDataContainer>()?.GetDataCollection<KeyTime>()?.Where(kt => kt.Key == "poll-send").FirstOrDefault()?.Time ?? new DateTime(1,1,1,19,0,0);
            _todayAutosendTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, _autosendTime.Hour, _autosendTime.Minute, 0);
            subscribeOnProvider();
        }

        private bool checkReady()
        {
            if (_context.Provider.SupervisorChatClient is null || _context.Provider.GroupChatClient is null) return false;
            return true;
        }

        private void autosendThreadTick()
        {
            while (!_autosendControl.Token.IsCancellationRequested)
            {
                if (!checkReady()) continue;
                if (Math.Abs((DateTime.Now - _todayAutosendTime).TotalMinutes) < 1 && !_isNextDayOpened)
                {
                    _mutex.WaitOne();
                    try
                    {
                        CloseDay();
                        OpenNextDay();
                    }
                    finally
                    {
                        _mutex.ReleaseMutex();
                        var tomorrowDateTime = DateTime.Now.AddDays(1);
                        _todayAutosendTime = new DateTime(tomorrowDateTime.Year, tomorrowDateTime.Month, tomorrowDateTime.Day, _autosendTime.Hour, _autosendTime.Minute, 0);
                    }
                }
                else if (Math.Abs((DateTime.Now - _todayAutosendTime.AddMinutes(1)).TotalMinutes) < 1 && _isNextDayOpened)
                {
                    _isNextDayOpened = false;
                }
                Thread.Sleep(60000);
            }
        }

        public void SendReportToSupervisor()
        {
            _context.Provider.SupervisorChatClient.SendMessage(new SendMessageArgs(createReport()));
        }

        public void SendReportToAdmin()
        {
            _context.Provider.AdminChatClient.SendMessage(new SendMessageArgs(createReport()));
        }
        private string createReport()
        {
            if ((_currentData?.DayActivitySchedule?.Count ?? 0) == 0) return "Нет статистики";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Статистика отсутствующих");
            builder.AppendLine();
            builder.AppendLine($"Дата: {_currentData.Date.ToString("dd.MM.yyyy")}");
            builder.AppendLine();
            for (int i = 0; i < _currentData.DayActivitySchedule.Count; i++)
            {
                builder.AppendLine($"[{_currentData.DayActivitySchedule[i].StartTime.ToString("HH:mm")}] - {_currentData.DayActivitySchedule[i].Name}");
                foreach (var student in _currentData.Students)
                {
                    if (!student.Statuses.ElementAt(i))
                    {
                        builder.AppendLine($"{_customUsers.Where(v => v.Id == student.Id).FirstOrDefault()?.FullName ?? student.Id}");
                    }
                }
                builder.AppendLine();
            }

            return builder.ToString();
        }
        public void CloseDay()
        {
            if (!checkReady()) return;
            SendReportToSupervisor();
            _context.Provider.GroupChatClient.DeleteMessage(new DeleteMessageArgs(_currentData?.PollMessageId ?? "0"));
        }

        public void OpenNextDay()
        {
            if (!checkReady()) return;
            _isNextDayOpened = true;
            _currentData = new PollVisitHandlerDayData();
            _currentData.Date = DateTime.Now.AddDays(1);
            _currentData.DayActivitySchedule = getDayActivities().ToList();
            _currentData.Students = new List<StudentVisitData>();
            var mes = sendPoll();
            _context.Provider.GroupChatClient.PinMessage(new PinMessageArgs(mes.Id));
            _currentData.PollId = mes.Poll.Id;
            _currentData.PollMessageId = mes.Id;
        }

        private Message sendPoll()
        {
            var args = createPollArgsForNextDay();
            return _context.Provider.GroupChatClient.SendPoll(args);
        }

        private IEnumerable<Activity> getDayActivities()
        {
            return _groupSchedule.GetDayEducationalActivities(_currentData.Date);
        }

        private SendPollArgs createPollArgsForNextDay()
        {
            string title = _title.Replace("{date}", _currentData.Date.ToString("dd.MM.yyyy"));
            List<string> options = new List<string>();
            foreach (var activity in _currentData.DayActivitySchedule)
            {
                options.Add($"[{activity.StartTime.ToString("HH:mm")}] {(activity as Activity).Name}");
            }
            var args = new SendPollArgs(title, options, false, true);
            return args;
        }

        private void adminUpdatedHandler(object sender, ClientChangedArgs args)
        {
            if (!(args.OldClient is null)) args.OldClient.MessageReceived -= adminMessageReceivedHandler;
            if (!(args.NewClient is null)) args.NewClient.MessageReceived += adminMessageReceivedHandler;
        }
        private void supervisorUpdatedHandler(object sender, ClientChangedArgs args)
        {
            if (!(args.OldClient is null)) args.OldClient.MessageReceived -= supervisorMessageReceivedHandler;
            if (!(args.NewClient is null)) args.NewClient.MessageReceived += supervisorMessageReceivedHandler;
        }
        private void groupUpdatedHandler(object sender, ClientChangedArgs args)
        {
            if (!(args.OldClient is null)) args.OldClient.PollAnswerReceived -= groupPollAnswerReceivedHandler;
            if (!(args.NewClient is null)) args.NewClient.PollAnswerReceived += groupPollAnswerReceivedHandler;
        }
        private void adminMessageReceivedHandler(object sender, Message message)
        {
            _mutex.WaitOne();
            try
            {

            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        private void supervisorMessageReceivedHandler(object sender, Message message)
        {
            _mutex.WaitOne();
            try
            {
                var messageText = message.Text;
                var match = _botCommandRegex.Match(messageText);
                if (!match.Success) return;
                switch (match.Groups["command"].Value.ToLower())
                {
                    case "стат":
                        SendReportToSupervisor();
                        break;
                    case "закрыть":
                        CloseDay();
                        OpenNextDay();
                        break;
                    default:
                        break;
                }
            }
            catch
            {

            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        private void groupPollAnswerReceivedHandler(object sender, PollAnswer answer)
        {
            _mutex.WaitOne();
            try
            {
                if (answer.PollId != _currentData.PollId) return;
                var studentVisitor = _currentData.Students.Where(v => v.Id == answer.Answerer.Id).FirstOrDefault();
                if (studentVisitor is null)
                {
                    studentVisitor = new StudentVisitData(answer.Answerer.Id, answer.Answerer.Username);
                    _currentData.Students.Add(studentVisitor);
                }

                studentVisitor.ResetStatuses(_currentData.DayActivitySchedule.Count);
                for (int i = 0; i < _currentData.DayActivitySchedule.Count; i++)
                {
                    studentVisitor.SetStatus(i, !answer.OptionIds.Contains(i));
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        private void subscribeOnProvider()
        {
            _context.Provider.SupervisorChatUpdated += supervisorUpdatedHandler;
            _context.Provider.AdminChatUpdated += adminUpdatedHandler;
            _context.Provider.GroupChatUpdated += groupUpdatedHandler;
        }
        private void unsubscribeFromProvider()
        {
            _context.Provider.SupervisorChatUpdated -= supervisorUpdatedHandler;
            _context.Provider.AdminChatUpdated -= adminUpdatedHandler;
            _context.Provider.GroupChatUpdated -= groupUpdatedHandler;
        }

        public IEnumerable<string> GetSupervisorCommands()
        {
            return _allCommands;
        }
    }
    class StudentVisitData : User
    {
        List<bool> _statuses;

        public StudentVisitData(string id, string username) : base(id, username)
        {
            _statuses = new List<bool>();
        }

        public IEnumerable<bool> Statuses => _statuses;
        public void ResetStatuses(int newAmount)
        {
            _statuses = new List<bool>();
            for (int i = 0; i < newAmount; i++) _statuses.Add(true);
        }

        public void SetStatus(int index, bool value)
        {
            if (index < 0 || index >= _statuses.Count) throw new IndexOutOfRangeException();
            _statuses[index] = value;
        }
    }
}
