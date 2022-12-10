using MireaChatBot.ChatHandlers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.DataContainers
{
    public interface IDataContainer
    {
        IGroupContainerBuilder Builder { get; }
        void SetBuilder(IGroupContainerBuilder builder);
        IEnumerable<T> GetDataCollection<T>() where T : class;
        IEnumerable<string> GetDataContainerCommands();
    }
}
