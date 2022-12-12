using MireaChatBot.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;

namespace MireaChatBot.Bot
{
    public class TelegramClient : IBotClient
    {
        public event EventHandler<Message> MessageReceived;
        public event EventHandler<IChatClient> ChatClientCreated;

        private BotClient _client;
        private List<string> _chatIds;
        private List<TelegramChatClient> _chatClients;
        private Thread _updateListenerThread;
        private CancellationTokenSource _threadControl;
        private int _lastHandledUpdateId;
        public TelegramClient(TelegramBotData data)
        {
            _client = new BotClient(data.Token);
            _chatIds = data.Chats?.ToList() ?? new List<string>();
            _chatClients = _chatIds.Select<string, TelegramChatClient>(chatId => new TelegramChatClient(chatId, _client)).ToList();
            _lastHandledUpdateId = data.LastHandledUpdateId;
        }
        public IChatClient GetChat(string chatId)
        {
            TelegramChatClient client = _chatClients.Where(chatClient => chatClient.ChatId == chatId).FirstOrDefault();
            if (client is null)
            {
                client = new TelegramChatClient(chatId, _client);
                _chatIds.Add(chatId);
                _chatClients.Add(client);
                ChatClientCreated?.Invoke(this, client);
            }
            return client;
        }

        public IEnumerable<string> GetChatIds()
        {
            return _chatIds;
        }

        public IEnumerable<IChatClient> GetChats()
        {
            return _chatClients;
        }

        public void StartWork()
        {
            _threadControl = new CancellationTokenSource();
            _updateListenerThread = new Thread(updateListenerTick);
            _updateListenerThread.Start();
        }

        public void StopWork()
        {
            _threadControl.Cancel();
        }

        private void updateListenerTick()
        {
            while (!_threadControl.Token.IsCancellationRequested)
            {
                Update[] updates = _client.GetUpdates(_lastHandledUpdateId + 1);
                foreach (var update in updates)
                {
                    if (!(update.Message is null) && update.Message.From.Id != _client.GetMe().Id)
                    {
                        var clientToNotify = GetChat(update.Message.Chat.Id.ToString());
                        Task.Run(() =>
                        {
                            var message = TelegramMessageConverter.ConvertFromTG(update.Message);
                            MessageReceived?.Invoke(this, message);
                        });
                        Task.Run(() =>
                        {
                            var message = TelegramMessageConverter.ConvertFromTG(update.Message);
                            clientToNotify.OnMessageReceived(message);
                        });
                    }
                    if (!(update.EditedMessage is null) && update.EditedMessage.From.Id != _client.GetMe().Id)
                    {
                        var clientToNotify = GetChat(update.EditedMessage.Chat.Id.ToString());
                        Task.Run(() =>
                        {
                            var message = TelegramMessageConverter.ConvertFromTG(update.EditedMessage);
                            clientToNotify.OnMessageReceived(message);
                        });
                    }
                    if (!(update.PollAnswer is null))
                    {
                        var pollAnswer = TelegramPollAnswerConverter.ConvertFromTG(update.PollAnswer);
                        foreach (var chatClient in _chatClients)
                        {
                            Task.Run(() =>
                            {
                                chatClient.OnPollAnswerReceived(pollAnswer);
                            });
                        }
                    }
                    _lastHandledUpdateId = _lastHandledUpdateId >= update.UpdateId ? _lastHandledUpdateId : update.UpdateId;
                }
                Thread.Sleep(100);
            }
        }
    }

    public class TelegramChatClient : IChatClient
    {
        private string _chatId;
        private BotClient _client;

        public TelegramChatClient(string chatId, BotClient client)
        {
            _chatId = chatId;
            _client = client;
        }

        public event EventHandler<Message> MessageReceived;
        public event EventHandler<Message> MessageEdited;
        public event EventHandler<PollAnswer> PollAnswerReceived;

        public string ChatId => _chatId;

        public IEnumerable<MemberAdmin> GetAllAdmins()
        {
            return _client.GetChatAdministrators<List<ChatMemberAdministrator>>(_chatId).Select<ChatMemberAdministrator, MemberAdmin>(chatAdmin => TelegramMemberAdminConverter.ConvertFromTG(chatAdmin));
        }

        public FileInfo GetFile(Document document)
        {
            TelegramFileDownloader.SetBotToken(_client.Token);
            return TelegramFileDownloader.DownloadFile(_client.GetFile(document.FileId), document.FileName);
        }

        public bool PinMessage(PinMessageArgs args)
        {
            return _client.PinChatMessage(Convert.ToInt64(_chatId), Convert.ToInt32(args.MessageId));
        }

        public Message SendMessage(SendMessageArgs args)
        {
            var tgArgs = new Telegram.BotAPI.AvailableMethods.SendMessageArgs(_chatId, args.MessageText);
            tgArgs.ReplyMarkup = TelegramReplyMarkupCreator.CreateCustomReplyMarkup(args.CustomReplyMarkup);
            var tgMessage = _client.SendMessage(tgArgs);
            var message = TelegramMessageConverter.ConvertFromTG(tgMessage);
            return message;
        }

        public Message SendMessage(SendFileArgs args)
        {
            if (args.File == "")
            {
                return SendMessage(args as SendMessageArgs);
            }
            byte[] buffer = System.IO.File.ReadAllBytes(args.File);
            string fileName = Path.GetFileName(args.File);
            var tgArgs = new SendDocumentArgs(_chatId, new InputFile(buffer, fileName));
            tgArgs.Caption = args.MessageText;
            var tgMessage = _client.SendDocument(tgArgs);
            var message = TelegramMessageConverter.ConvertFromTG(tgMessage);
            return message;
        }
        public bool DeleteMessage(DeleteMessageArgs args)
        {
            try
            {
                return _client.DeleteMessage(_chatId, Convert.ToInt32(args.MessageId));
            }
            catch
            {
                return false;
            }
        }

        public Message SendPoll(SendPollArgs args)
        {
            var tgArgs = new Telegram.BotAPI.AvailableMethods.SendPollArgs(_chatId, args.Title, args.Options);
            tgArgs.IsAnonymous = args.IsAnonymous;
            tgArgs.AllowsMultipleAnswers = args.MultipleChoiceAvailable;
            var tgMessage = _client.SendPoll(tgArgs);
            var message = TelegramMessageConverter.ConvertFromTG(tgMessage);
            return message;
        }

        public void OnMessageReceived(Message message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public void OnMessageEdited(Message message)
        {
            MessageEdited?.Invoke(this, message);
        }

        public void OnPollAnswerReceived(PollAnswer pollAnswer)
        {
            PollAnswerReceived?.Invoke(this, pollAnswer);
        }

        public bool StopPoll(StopPollArgs args)
        {
            var tgPoll = _client.StopPoll(Convert.ToInt64(_chatId), Convert.ToInt32(args.MessageId));
            return !(tgPoll is null);
        }

        public IEnumerable<Member> GetAllMembers()
        {
            throw new NotImplementedException();
        }
    }

    public class TelegramBotData
    {
        private string _token;
        private List<string> _chats;
        private int _lastHandledId;
        public TelegramBotData(string token, List<string> chats, int lastHandledId)
        {
            _token = token;
            _chats = chats;
            _lastHandledId = lastHandledId;
        }

        public IEnumerable<string> Chats => _chats;
        public string Token => _token;
        public int LastHandledUpdateId => _lastHandledId;
    }

    public static class TelegramFileDownloader
    {
        private static readonly string _downloadUrl = "https://api.telegram.org/file/bot";
        private static readonly string _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MireaChatBot", "Downloads");
        private static string _token;
        public static void SetBotToken(string token)
        {
            _token = token;
        }

        public static FileInfo DownloadFile(Telegram.BotAPI.AvailableTypes.File file, string fileName)
        {
            var url = _downloadUrl + _token + "/" + file.FilePath;
            byte[] buffer = HTTPClient.GetBytes(url);
            string pathToSave = Path.Combine(_downloadPath, fileName);
            createDirIfNotExistes();
            System.IO.File.WriteAllBytes(pathToSave, buffer);
            FileInfo info = new FileInfo(pathToSave);
            return info;
        }

        private static void createDirIfNotExistes()
        {
            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }
    }

    public static class TelegramMessageConverter
    {
        public static Message ConvertFromTG(Telegram.BotAPI.AvailableTypes.Message message)
        {
            if (message is null) return null;
            var message_ = new Message(message.MessageId.ToString(), message.Text ?? "",
                TelegramChatConverter.ConvertFromTG(message.Chat), TelegramUserConverter.ConvertFromTG(message.From),
                TelegramPollConverter.ConvertFromTG(message.Poll), TelegramMessageConverter.ConvertFromTG(message.ReplyToMessage),
                TelegramDocumentConverter.ConvertFromTG(message.Document));
            return message_;
        }
    }

    public static class TelegramChatConverter
    {
        public static Chat ConvertFromTG(Telegram.BotAPI.AvailableTypes.Chat chat)
        {
            if (chat is null) return null;
            var chat_ = new Chat(chat.Id.ToString(), chat.Title, convertChatTypeFromTG(chat));
            return chat_;
        }

        private static ChatType convertChatTypeFromTG(Telegram.BotAPI.AvailableTypes.Chat chat)
        {
            if (chat.Type == "private") return ChatType.PRIVATE;
            return ChatType.GROUP;
        }
    }
    public static class TelegramUserConverter
    {
        public static User ConvertFromTG(Telegram.BotAPI.AvailableTypes.User user)
        {
            if (user is null) return null;
            var user_ = new User(user.Id.ToString(), user.Username);
            return user_;
        }
    }
    public static class TelegramPollConverter
    {
        public static Poll ConvertFromTG(Telegram.BotAPI.AvailableTypes.Poll poll)
        {
            if (poll is null) return null;
            var poll_ = new Poll(poll.Id, poll.Question, poll.Options.Select<PollOption, string>(option => option.Text.ToString()),
                poll.IsAnonymous, poll.AllowsMultipleAnswers);
            return poll_;
        }
    }

    public static class TelegramDocumentConverter
    {
        public static Document ConvertFromTG(Telegram.BotAPI.AvailableTypes.Document document)
        {
            if (document is null) return null;
            var document_ = new Document(document.FileId, document.FileName);
            return document_;
        }
    }

    public static class TelegramPollAnswerConverter
    {
        public static PollAnswer ConvertFromTG(Telegram.BotAPI.AvailableTypes.PollAnswer pollAnswer)
        {
            if (pollAnswer is null) return null;
            var pollAnswer_ = new PollAnswer(TelegramUserConverter.ConvertFromTG(pollAnswer.User),
                pollAnswer.PollId, pollAnswer.OptionIds.Select<uint, int>(optionId => (int)optionId));
            return pollAnswer_;
        }
    }

    public static class TelegramMemberAdminConverter
    {
        public static MemberAdmin ConvertFromTG(Telegram.BotAPI.AvailableTypes.ChatMemberAdministrator member)
        {
            if (member is null) return null;
            var member_ = new MemberAdmin(TelegramUserConverter.ConvertFromTG(member.User), member.CustomTitle);
            return member_;
        }
    }
    public static class TelegramMemberConverter
    {
        public static Member ConvertFromTG(Telegram.BotAPI.AvailableTypes.ChatMember member)
        {
            if (member is null) return null;
            var member_ = new Member(TelegramUserConverter.ConvertFromTG(member.User));
            return member_;
        }
    }

    public static class TelegramReplyMarkupCreator
    {
        public static Telegram.BotAPI.AvailableTypes.ReplyMarkup CreateCustomReplyMarkup(ReplyMarkup markup)
        {
            if (markup is null || markup == ReplyMarkup.NoMarkup) return new ReplyKeyboardRemove();
            ReplyKeyboardMarkup tgMarkup = new ReplyKeyboardMarkup();
            List<List<KeyboardButton>> tgButtons = new List<List<KeyboardButton>>();
            foreach(var buttonCollection in markup.Buttons)
            {
                tgButtons.Add(new List<KeyboardButton>());
                foreach(var button in buttonCollection)
                {
                    KeyboardButton tgButton = new KeyboardButton(button);
                    tgButtons.Last().Add(tgButton);
                }
            }
            tgMarkup.Keyboard = tgButtons;
            return tgMarkup;
        }
    }
}