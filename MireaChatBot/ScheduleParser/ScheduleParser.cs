using System.Collections.Generic;

namespace MireaChatBot.ScheduleParsers
{
    public interface GroupScheduleParser
    {
        IEnumerable<GroupSchedule> Parse();
    }
}
