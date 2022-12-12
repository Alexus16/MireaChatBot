using MireaChatBot.GroupContainers;
using MireaChatBot.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.RepositoryBuilders
{
    public class GroupBuilderRepositoryBuilder
    {
        private IRepository _repo;

        public GroupContainerBuilder Build()
        {
            if (_repo is null) throw new InvalidOperationException("Trying to extract data from NULL repo");
            var data = _repo.Load();
            return null;
        }

        private bool checkAllBuilderKeys(IEnumerable<string> keys)
        {
            if(!keys.Contains("DataContainers")) return false;
            if(!keys.Contains("GroupContainers")) return false;
            if(!keys.Contains("BotHandlers")) return false;
            if(!keys.Contains("Registrator")) return false;
            if(!keys.Contains("")) return false;
            if(!keys.Contains("DataContainers")) return false;
            if(!keys.Contains("DataContainers")) return false;
            return true;
        }
    }
}
