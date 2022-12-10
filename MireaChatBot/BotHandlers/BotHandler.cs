using MireaChatBot.ChatHandlers;
using MireaChatBot.GroupContainers;
using System.Text.RegularExpressions;

namespace MireaChatBot.BotHandlers
{
    public interface ICommonChatHandler
    {
        IGroupContainerBuilder Builder { get; }
        void SetBuilder(IGroupContainerBuilder builder);
    }
}
