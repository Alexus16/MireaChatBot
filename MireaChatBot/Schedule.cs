using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot
{

    public interface Activity
    {
        string Name { get; }
        DateTime StartTime { get; }
        TimeSpan Duration { get; }
        Guid ActivityId { get; }

    }

    public interface SubjectInfo
    {
        string Name { get; }
    }

    public enum LessonType
    {
        UNDEFINED = 0,
        LAB = 1,
        PRACTIC = 2,
        LECTION = 3,
        AUTONOMOUS_WORK = 4,
    }

    public interface Subject : SubjectInfo
    {
        Employee Teacher { get; }
        string Auditory { get; }
        LessonType LessonType { get; }
    }

    public interface EducationalActivity : Subject, Activity { }

    public interface Employee
    {
        string FirstName { get; }
        string LastName { get; }
        string FullName { get; }
    }

    public interface Schedule
    {
        IEnumerable<EducationalActivityDescriber> EducationalActivityDescribers { get; }
        IEnumerable<EducationalActivity> GetDayEducationalActivities(DateTime date);
    }
    public interface Group
    {
        string Name { get; }
    }

    public interface GroupSchedule : Schedule
    {
        Group Group { get; }
    }

    public interface EducationalActivityDescriber
    {
        EducationalActivity Activity { get; }
        IEnumerable<int> ActiveWeeks { get; set; }
        DayOfWeek WeekDay { get; }
    }
}
