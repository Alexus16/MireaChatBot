using MireaChatBot.ChatHandlers;
using MireaChatBot.GroupContainers;
using MireaChatBot.ScheduleAccessors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.DataContainers
{
    public class ScheduleDataContainer : IDataContainer
    {
        private IGroupContainerBuilder _builder;
        public IGroupContainerBuilder Builder => _builder;

        private GroupScheduleAccessor _scheduleAccessor;
        private IEnumerable<GroupSchedule> _allSchedules => _scheduleAccessor?.GetAllSchedules() ?? throw new InvalidOperationException("Не установлен парсер данных");
        public ScheduleDataContainer(GroupScheduleAccessor scheduleAccessor)
        {
            _builder = null;
            _scheduleAccessor = scheduleAccessor;
        }

        public IEnumerable<T> GetDataCollection<T>() where T : class
        {
            return _allSchedules.Select<GroupSchedule, T>(gs => gs is T ? gs as T : default(T));
        }

        public void SetBuilder(IGroupContainerBuilder builder)
        {
            _builder = builder;
        }

        public IEnumerable<string> GetDataContainerCommands()
        {
            return null;
        }
    }
}
