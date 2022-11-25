using MireaChatBot.ScheduleParsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot
{
    public class MireaGroupScheduleFactory
    {
        private MireaGroupFactory _groupFactory;
        public MireaGroupSchedule CreateEmptySchedule(string groupName)
        {
            if (_groupFactory is null) _groupFactory = new MireaGroupFactory();
            return new MireaGroupSchedule(_groupFactory.Create(groupName));
        }
    }

    public class MireaLessonDescriberFactory
    {
        public MireaLessonDescriber Create(EducationalActivity activity, List<int> activeWeeks, DayOfWeek weekDay)
        {
            return new MireaLessonDescriber(activity, activeWeeks, weekDay);
        }
    }

    public class MireaLessonDescriber : EducationalActivityDescriber
    {
        private EducationalActivity _activity;
        private List<int> _activeWeeks;
        private DayOfWeek _weekDay;
        public MireaLessonDescriber(EducationalActivity activity, List<int> activeWeeks, DayOfWeek weekDay)
        {
            _activity = activity;
            _activeWeeks = activeWeeks.AsEnumerable().ToList();
            _weekDay = weekDay;
        }
        public EducationalActivity Activity => _activity;
        public IEnumerable<int> ActiveWeeks
        {
            get => _activeWeeks;
            set => _activeWeeks = value.ToList();
        }
        public DayOfWeek WeekDay => _weekDay;
    }

    public class MireaGroupSchedule : GroupSchedule
    {
        private UniversityGroup _group;
        private List<MireaLessonDescriber> _mireaLessonDescribers;
        
        public MireaGroupSchedule(UniversityGroup group)
        {
            _group = group;
        }
        public void SetDescribers(IEnumerable<MireaLessonDescriber> describers)
        {
            _mireaLessonDescribers = describers.ToList();
        }
        public Group Group => _group;
        public IEnumerable<EducationalActivityDescriber> EducationalActivityDescribers => _mireaLessonDescribers;
        public IEnumerable<EducationalActivity> GetDayEducationalActivities(DateTime date)
        {
            List<EducationalActivity> dayActivities = new List<EducationalActivity>();
            MireaDateHelper.SetSemester(SemesterSeason.AUTUMN);
            foreach (var describer in _mireaLessonDescribers.Where(describer => describer.WeekDay == date.DayOfWeek
            && describer.ActiveWeeks.Contains(MireaDateHelper.GetWeekNumberOfDay(date))))
            {
                dayActivities.Add(describer.Activity);
            }
            return dayActivities;
        }
    }
}
