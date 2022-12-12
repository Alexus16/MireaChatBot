using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace MireaChatBot.Repositories
{
    public interface IRepository
    {
        void Save(IRepositoryData data);
        IRepositoryData Load();
    }
    public interface IRepositoryData
    {
        Dictionary<string, object> Data { get; set; }
    }

    public class FileRepository : IRepository
    {
        private const string _fileName = "data.json";
        private string _repoPath;
        public string RepoPath => _repoPath;
        private string _filePath => Path.Combine(_repoPath, _fileName);

        public IRepositoryData Load()
        {
            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrEmpty(json)) return null;
            IRepositoryData data = JsonConvert.DeserializeObject<IRepositoryData>(json);
            return data;
        }

        public void Save(IRepositoryData data)
        {
            if (data is null) return;
            string json = JsonConvert.SerializeObject(data);
            if(!Directory.Exists(_repoPath)) Directory.CreateDirectory(_repoPath);
            File.WriteAllText(_filePath, json);
        }
    }
}