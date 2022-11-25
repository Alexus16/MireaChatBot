using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MireaChatBot
{
    public class MireaGroupFactory
    {
        public UniversityGroup Create(string groupName)
        {
            Regex groupNameRegex = new Regex(@"(\w)(\w)(\w)(\w)-(\d)(\d)-(\d)(\d)");
            if (!groupNameRegex.IsMatch(groupName)) throw new ArgumentException("Несоответствие паттерну имени группы МИРЭА", nameof(groupName));
            return new UniversityGroup(groupNameRegex.Match(groupName).Value);
        }
    }

    public class UniversityGroup : Group
    {
        private string _name;

        public UniversityGroup(string name)
        {
            _name = name;
        }
        public string Name => _name;
    }
}
