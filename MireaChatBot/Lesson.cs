using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot
{
    public class MireaLessonFactory
    {
        public Lesson Create(string lessonName, string auditory, DateTime startTime, LessonType lessonType, Employee teacher = null)
        {
            var lesson = new Lesson(lessonName, teacher, startTime, new TimeSpan(1, 30, 0), lessonType, auditory);
            return lesson;
        }
    }

    public class Lesson : EducationalActivity
    {
        string _name;
        string _auditory;
        Employee _teacher;
        DateTime _startTime;
        TimeSpan _duration;
        LessonType _lessonType;
        Guid _activityId;
        public Lesson(string name, Employee teacher, DateTime startTime, TimeSpan duration, LessonType lessonType, string auditory)
        {
            _name = name;
            _teacher = teacher;
            _startTime = startTime;
            _duration = duration;
            _lessonType = lessonType;
            _auditory = auditory;
        }
        public string Name => _name;
        public string Auditory => _auditory;
        public Employee Teacher => _teacher;
        public DateTime StartTime => _startTime;
        public TimeSpan Duration => _duration;
        public LessonType LessonType => _lessonType;
        public Guid ActivityId => _activityId;
    }
}
