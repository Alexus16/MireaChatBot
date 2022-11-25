using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.ScheduleRepositories
{
    public interface GroupScheduleRepository
    {
        IEnumerable<GroupSchedule> GetAll();
        void Save(IEnumerable<GroupSchedule> data);
        bool HasData { get; }
    }
}
