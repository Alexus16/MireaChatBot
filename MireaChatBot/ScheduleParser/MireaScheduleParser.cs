using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.ScheduleParser
{
    public class MireaScheduleParser
    {
        
    }

    public class SemiSchedule
    {
        private List<WeekSchedule> _weeks;
    }

    public class WeekSchedule
    {
        private List<DaySchedule> _days;

    }

    public class DaySchedule
    {
        private List<Lesson> _lessons;
        private DaySchedule() { }
        public int AmountOfLessons => _lessons.Count;
        
        public static DaySchedule Create()
        {
            DaySchedule daySchedule = new DaySchedule();
            return daySchedule;
        }
    }

    public class Lesson
    {
        public LessonInfo Info { get; set; }
        public SubjectInfo Subject { get; set; }
    
    }

    public class LessonInfo
    {
        public DateTime StartTime { get; set; } 
        public DateTime EndTime { get; set; }
        public int Number { get; set; }
        public LessonInfo(DateTime startTime, DateTime endTime, int number)
        {
            Number = number;
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    public class SubjectInfo
    {
        public string SubjectName { get; set; }
        public string Auditory { get; set; }
        public LESSON_TYPE Type { get; set; }
    }

    public class SubjectInfoFactory
    {
        public string 
    }

    public class SubjectInfoParser
    {
    
    }

    public enum LESSON_TYPE
    {
        Undefined = 0,
        Lecture = 1,
        Seminary = 2,
        Lab = 3,
    }

    public class LessonInfoDayFactory
    {
        private LessonInfo[] _lessons;
        private LessonInfoDayFactory() { }

        public static LessonInfoDayFactory CreateFactory(IDayConfiguration configuration)
        {
            LessonInfoDayFactory factory = new LessonInfoDayFactory();
            factory._lessons = generateLessons(configuration);
            return factory;
        }

        private static LessonInfo[] generateLessons(IDayConfiguration configuration)
        {
            LessonInfo[] lessons = new LessonInfo[configuration.LessonAmount];
            LessonInfo prevLesson = null;
            for (int i = 0; i < lessons.Length; i++)
            {
                prevLesson = (lessons[i] = generateNextLesson(configuration, prevLesson));
            }
            return lessons;
        }

        private static LessonInfo generateNextLesson(IDayConfiguration configuration, LessonInfo prevLesson = null)
        {
            if(prevLesson == null)
            {
                return generateFirstLesson(configuration);
            }
            int prevLessonNumber = prevLesson.Number;
            int breakDuration = configuration.BreakDurations[prevLessonNumber - 1];
            LessonInfo info = new LessonInfo(prevLesson.EndTime.AddMinutes(breakDuration), prevLesson.EndTime.AddMinutes(configuration.LessonDuration + breakDuration), prevLessonNumber + 1);
            return info;
        }

        private static LessonInfo generateFirstLesson(IDayConfiguration configuration)
        {
            return new LessonInfo(configuration.StartTime, configuration.StartTime.AddMinutes(configuration.LessonDuration), 1);
        }

        public LessonInfo GetInfoByLessonNumber(int lessonNumber)
        {
            try
            {
                return _lessons[lessonNumber - 1];
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidOperationException("No lesson with such number existed");
            }
        }
        
        public LessonInfo this[int number]
        {
            get
            {
                return GetInfoByLessonNumber(number);
            }
        }
    }

    public interface IDayConfiguration
    {
        DateTime StartTime { get; }
        int LessonDuration { get; }
        int[] BreakDurations { get; }
        int LessonAmount { get; }
    }

    public class DayConfiguration : IDayConfiguration
    {
        private int[] _breakDurations;

        public DayConfiguration(int lessonAmount)
        {
            _breakDurations = new int[lessonAmount - 1];
            StartTime = new DateTime();
            LessonDuration = 0;
        }
        public DateTime StartTime { get; private set; }
        public int LessonDuration { get; private set; }
        public int[] BreakDurations { get; private set; }
        public int LessonAmount => _breakDurations.Length + 1;

        DayConfiguration SetLessonDuration(int duration)
        {
            LessonDuration = duration;
            return this;
        }

        DayConfiguration SetBreakDuration(int breakNumber, int breakDuration)
        {
            if (breakNumber >= _breakDurations.Length) throw new IndexOutOfRangeException("Got break number out of range");
            _breakDurations[breakNumber - 1] = breakDuration;
            return this;
        }

        DayConfiguration SetStartTime(DateTime startTime)
        {
            StartTime = startTime;
            return this;
        }
    }
}
