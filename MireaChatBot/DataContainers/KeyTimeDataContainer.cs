using MireaChatBot.ChatHandlers;
using MireaChatBot.GroupContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.DataContainers
{
    public class KeyTimeDataContainer : IDataContainer
    {
        private IGroupContainerBuilder _builder;
        private List<KeyTime> _keyTimes;
        public KeyTimeDataContainer()
        {
            _keyTimes = new List<KeyTime>();
        }
        public IGroupContainerBuilder Builder => _builder;

        IGroupContainerBuilder IDataContainer.Builder => throw new NotImplementedException();

        public IEnumerable<T> GetDataCollection<T>() where T : class
        {
            return _keyTimes.Select<KeyTime, T>(kt => kt is T ? kt as T : default(T));
        }

        public void SetBuilder(IGroupContainerBuilder builder)
        {
            _builder = builder;
        }

        public void AddKeyTime(string key, DateTime time)
        {
            var keytime = new KeyTime(key, time);
            AddKeyTime(keytime);
        }

        public void AddKeyTime(KeyTime keyTime)
        {
            if (_keyTimes.Any(kt => kt.Key == keyTime.Key)) return;
            _keyTimes.Add(keyTime);
        }

        public IEnumerable<string> GetDataContainerCommands()
        {
            return null;
        }
    }

    public class KeyTime
    {
        private string _key;
        private DateTime _time;
        public KeyTime(string key, DateTime time)
        {
            _key = key;
            _time = time;
        }

        public string Key => _key;
        public DateTime Time => _time;
    }
}
