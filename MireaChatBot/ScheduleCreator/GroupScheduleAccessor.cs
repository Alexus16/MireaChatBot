using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.ScheduleAccessors
{
    public interface GroupScheduleAccessor
    {
        GroupSchedule GetSchedule(string groupName);
        IEnumerable<GroupSchedule> GetAllSchedules();
    }

    public interface GroupScheduleUpdateableAccessor : GroupScheduleAccessor
    {
        void Update();
    }
}
