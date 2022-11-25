using MireaChatBot.ScheduleAccessors;
using MireaChatBot.ScheduleParsers;
using MireaChatBot.ScheduleRepositories;
using System.Collections.Generic;
using System.Linq;

namespace MireaChatBot.ScheduleCreator
{
    public class ParserAccessor : GroupScheduleAccessor
    {
        private GroupScheduleParser _parser;
        public ParserAccessor(GroupScheduleParser parser)
        {
            _parser = parser;
        }

        public GroupSchedule GetSchedule(string groupName)
        {
            return _parser.Parse().Where(_groupSchedule => _groupSchedule.Group.Name.ToLower() == groupName.ToLower()).FirstOrDefault();
        }

        public IEnumerable<GroupSchedule> GetAllSchedules()
        {
            return _parser.Parse();
        }
    }

    public class RepositoryAccessor : GroupScheduleUpdateableAccessor
    {
        private GroupScheduleRepository _repository;
        private ParserAccessor _parserAccessor;

        public RepositoryAccessor(GroupScheduleRepository repository, GroupScheduleParser parser)
        {
            _repository = repository;
            _parserAccessor = new ParserAccessor(parser);
        }

        public GroupSchedule GetSchedule(string groupName)
        {
            saveFromParserToEmptyRepository();
            return _repository.GetAll().Where(groupSchedule => groupSchedule.Group.Name.ToLower() == groupName.ToLower()).FirstOrDefault();
        }

        public IEnumerable<GroupSchedule> GetAllSchedules()
        {
            saveFromParserToEmptyRepository();
            return _repository.GetAll();
        }

        public void Update()
        {
            saveFromParser();
        }

        private void saveFromParser()
        {
            IEnumerable<GroupSchedule> schedules = _parserAccessor.GetAllSchedules();
            _repository.Save(schedules);
        }

        private void saveFromParserToEmptyRepository()
        {
            if (!_repository.HasData) saveFromParser();
        }
    }

    public class CacheAccessorWithoutDB : GroupScheduleUpdateableAccessor
    {
        private ParserAccessor _parserAccessor;
        private List<GroupSchedule> _cachedSchedules;
        private bool hasData => _cachedSchedules.Count > 0;
        public CacheAccessorWithoutDB(GroupScheduleParser parser)
        {
            _parserAccessor = new ParserAccessor(parser);
            _cachedSchedules = new List<GroupSchedule>();
        }
        public GroupSchedule GetSchedule(string groupName)
        {
            cacheIfEmpty();
            return _cachedSchedules.Where(groupSchedule => groupSchedule.Group.Name.ToLower() == groupName.ToLower()).FirstOrDefault();
        }

        public IEnumerable<GroupSchedule> GetAllSchedules()
        {
            cacheIfEmpty();
            return _cachedSchedules;
        }

        public void Update()
        {
            cache();
        }

        private void cache()
        {
            _cachedSchedules = _parserAccessor.GetAllSchedules().ToList();
        }

        private void cacheIfEmpty()
        {
            if (!hasData) cache();
        }
    }

    public class CacheAccessor : GroupScheduleUpdateableAccessor
    {
        private RepositoryAccessor _repositoryAccessor;
        private List<GroupSchedule> _cachedSchedules;
        private bool hasData => _cachedSchedules.Count > 0;
        public CacheAccessor(GroupScheduleRepository repository, GroupScheduleParser parser)
        {
            _repositoryAccessor = new RepositoryAccessor(repository, parser);
            _cachedSchedules = new List<GroupSchedule>();
        }

        public GroupSchedule GetSchedule(string groupName)
        {
            cacheIfEmpty();
            return _cachedSchedules.Where(groupSchedule => groupSchedule.Group.Name.ToLower() == groupName.ToLower()).FirstOrDefault();
        }

        public IEnumerable<GroupSchedule> GetAllSchedules()
        {
            cacheIfEmpty();
            return _cachedSchedules;
        }

        public void Update()
        {
            _repositoryAccessor.Update();
            cache();
        }

        private void cache()
        {
            _cachedSchedules = _repositoryAccessor.GetAllSchedules().ToList();
        }

        private void cacheIfEmpty()
        {
            if (!hasData) cache();
        }
    }
}
