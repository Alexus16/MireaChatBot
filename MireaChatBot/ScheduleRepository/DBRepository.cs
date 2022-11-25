using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MireaChatBot.Postgres;
using Npgsql;


namespace MireaChatBot.ScheduleRepositories
{
    public class PostgresScheduleRepository : GroupScheduleRepository
    {
        private PostgresClient _dbClient;
        public bool HasData => false;
        public IEnumerable<GroupSchedule> GetAll()
        {
            throw new NotImplementedException();
        }

        public void Save(IEnumerable<GroupSchedule> data)
        {
            throw new NotImplementedException();
        }
    }
}
