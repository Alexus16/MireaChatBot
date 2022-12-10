using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.UpdatingMessages;

public interface IBotClient
{
    event EventHandler<Message> MessageReceived;
    event EventHandler<IChatClient> ChatClientCreated;
    IEnumerable<string> GetChatIds();
    IEnumerable<IChatClient> GetChats();
    IChatClient GetChat(string chatId);
    void StartWork();
    void StopWork();
}

public interface IChatClient
{
    string ChatId { get; }
    event EventHandler<Message> MessageReceived;
    event EventHandler<Message> MessageEdited;
    event EventHandler<PollAnswer> PollAnswerReceived;
    IEnumerable<MemberAdmin> GetAllAdmins();
    IEnumerable<Member> GetAllMembers();
    Message SendMessage(SendMessageArgs args);
    Message SendMessage(SendFileArgs args);
    bool PinMessage(PinMessageArgs args);
    Message SendPoll(SendPollArgs args);
    bool StopPoll(StopPollArgs args);
    bool DeleteMessage(DeleteMessageArgs args);
    FileInfo GetFile(Document document);

    void OnMessageReceived(Message message);
    void OnMessageEdited(Message message);
    void OnPollAnswerReceived(PollAnswer pollAnswer);
    
}


public class Message
{
    public Message(string id, string text, Chat chat, User from, Poll poll, Message replyTo, Document document)
    {
        Id = id;
        Text = text;
        Chat = chat;
        From = from;
        Poll = poll;
        ReplyTo = replyTo;
        Document = document;
    }

    public string Id { get; }
    public string Text { get; }
    public Chat Chat { get; }
    public User From { get; }
    public Poll Poll { get; }
    public Message ReplyTo { get; }
    public Document Document { get; }
}

public class Chat
{
    public Chat(string id, string title, ChatType type)
    {
        Id = id;
        Title = title;
        Type = type;
    }
    public string Id { get; }
    public string Title { get; }
    public ChatType Type { get; }
}

public class User
{
    public User(string id, string username)
    {
        Id = id;
        Username = username;
    }

    public string Id { get; }
    public string Username { get; }
}

public class MemberAdmin
{
    public MemberAdmin(User user, string customTitle)
    {
        User = user;
        CustomTitle = customTitle;
    }

    public User User { get; }
    public string CustomTitle { get; }
}

public class Member
{
    public Member(User user)
    {
        User = user;
    }
    public User User { get; }
}

public abstract class SendArgs { }

public class SendMessageArgs : SendArgs
{
    public SendMessageArgs(string messageText) : this(messageText, ReplyMarkup.NoMarkup) { }
    public SendMessageArgs(string messageText, ReplyMarkup customReplyMarkup)
    {
        MessageText = messageText;
        CustomReplyMarkup = customReplyMarkup;
    }

    public string MessageText { get; }
    public ReplyMarkup CustomReplyMarkup { get; }
}

public class SendFileArgs : SendMessageArgs
{
    public SendFileArgs(string text, string file) : this(text, file, ReplyMarkup.NoMarkup) { }
    public SendFileArgs(string text, string file, ReplyMarkup customReplyMarkup) : base(text, customReplyMarkup)
    {
        File = file;
    }

    public string File { get; }
}

public class PinMessageArgs : SendArgs
{
    public PinMessageArgs(string messageId)
    {
        MessageId = messageId;
    }

    public string MessageId { get; }
}

public class DeleteMessageArgs
{
    public DeleteMessageArgs(string messageId)
    {
        MessageId = messageId;
    }

    public string MessageId { get; }
}

public class SendPollArgs : SendArgs
{
    public SendPollArgs(string title, IEnumerable<string> options, bool isAnonymous, bool multipleChoiceAvailable)
    {
        Title = title;
        Options = options;
        IsAnonymous = isAnonymous;
        MultipleChoiceAvailable = multipleChoiceAvailable;
    }

    public string Title { get; }
    public IEnumerable<string> Options { get; }
    public bool IsAnonymous { get; }
    public bool MultipleChoiceAvailable { get; }
}

public class StopPollArgs
{
    public StopPollArgs(string chatId, string messageId)
    {
        ChatId = chatId;
        MessageId = messageId;
    }

    public string ChatId { get; }
    public string MessageId { get; }
}

public class PollAnswer
{
    public PollAnswer(User answerer, string pollId, IEnumerable<int> optionIds)
    {
        Answerer = answerer;
        PollId = pollId;
        OptionIds = optionIds;
    }

    public User Answerer { get; }
    public string PollId { get; }
    public IEnumerable<int> OptionIds { get; }
}

public class Poll
{
    public Poll(string id, string title, IEnumerable<string> options, bool isAnonymous, bool multipleChoiceAvailable)
    {
        Id = id;
        Title = title;
        Options = options;
        IsAnonymous = isAnonymous;
        MultipleChoiceAvailable = multipleChoiceAvailable;
    }

    public string Id { get; }
    public string Title { get; }
    public IEnumerable<string> Options { get; }
    public bool IsAnonymous { get; }
    public bool MultipleChoiceAvailable { get; }
}

public class ReplyMarkup
{
    public IEnumerable<IEnumerable<string>> Buttons { get; }
    private ReplyMarkup(IEnumerable<IEnumerable<string>> buttons)
    {
        Buttons = buttons;
    }

    private static ReplyMarkup _noMarkup;
    public static ReplyMarkup NoMarkup => (_noMarkup ?? (_noMarkup = new ReplyMarkup(null)));

    public static ReplyMarkup Create(IEnumerable<string> buttonTexts)
    {
        List<List<string>> buttons = new List<List<string>>();
        int it = 0;
        foreach(var buttonText in buttonTexts)
        {
            if (it++ % 3 == 0) buttons.Add(new List<string>());
            buttons.Last().Add(buttonText);
        }
        var markup = new ReplyMarkup(buttons);
        return markup;
    }
}

public class Document
{
    public Document(string fileId, string fileName)
    {
        FileId = fileId;
        FileName = fileName;
    }
    public string FileId { get; }
    public string FileName { get; }
}

public enum ChatType
{
    GROUP,
    PRIVATE,
}