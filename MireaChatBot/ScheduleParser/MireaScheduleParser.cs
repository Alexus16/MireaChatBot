using DocumentFormat.OpenXml.Office.CustomUI;
using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;
using OfficeOpenXml.Drawing.Chart;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MireaChatBot.ScheduleParsers
{
    public class MireaScheduleParser : GroupScheduleParser
    {
        private readonly string downloadScheduleUrl = @"https://www.mirea.ru/schedule/";
        private MireaExcelScheduleExtractor _extractor = new MireaExcelScheduleExtractor();
        public IEnumerable<GroupSchedule> Parse()
        {
            string[] downloadedFilePathes = downloadAllScheduleFiles();
            List<GroupSchedule> allSchedules = new List<GroupSchedule>();
            foreach (var downloadedFilePath in downloadedFilePathes)
            {
                allSchedules.AddRange(_extractor.ExtractFromFile(downloadedFilePath));
            }
            return allSchedules;
        }

        private string[] downloadAllScheduleFiles()
        {
            string folderPath = Path.Combine(Path.GetTempPath(), HashCalculator.GetHashString(downloadScheduleUrl));
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            List<string> filePathes = new List<string>();
            string htmlContent = HTTPClient.GetString(downloadScheduleUrl);
            string[] allDownloadUrls = HTMLHelper.GetAllAttributeValues(htmlContent, "href");
            foreach (string url in allDownloadUrls)
            {
                if (url.Contains("webservice"))
                {
                    string filePath = Path.Combine(folderPath, $"{HashCalculator.GetHashString(url)}.xlsx");
                    File.WriteAllBytes(filePath, HTTPClient.GetBytes(url));
                    filePathes.Add(filePath);
                }
            }
            return filePathes.ToArray();
        }
    }

    internal static class HashCalculator
    {
        private static MD5 _md5;
        static HashCalculator()
        {
            _md5 = MD5.Create();
        }
        public static string GetHashString(string initValue)
        {
            byte[] initArr = Encoding.UTF8.GetBytes(initValue);
            byte[] hash = _md5.ComputeHash(initArr, 0, initArr.Length);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }


    internal class MireaExcelScheduleExtractor
    {
        private MireaGroupScheduleFactory _scheduleFactory = new MireaGroupScheduleFactory();
        private MireaLessonFactory _lessonFactory = new MireaLessonFactory();
        private MireaLessonDescriberFactory _lessonDescriberFactory = new MireaLessonDescriberFactory();
        private MireaSubjectExtractor _subjectExtractor = new MireaSubjectExtractor();

        private readonly int lessonLimit = 7;
        private readonly int rowLimit = 100;

        private readonly Regex groupBlockStartRegex = new Regex(@"\w\w\w\w-\d\d-\d\d");
        private readonly int groupSearchDepth = 20;

        private readonly int dayNameColumn = 1;
        private readonly int lessonNumberColumn = 2;
        private readonly int startTimeColumn = 3;
        private readonly int weekNumberColumn = 5;

        private readonly int groupNameRow = 2;

        private readonly int disciplineColumnOffset = 0;
        private readonly int lessonTypeColumnOffset = 1;
        private readonly int teacherColumnOffset = 2;
        private readonly int auditoryColumnOffset = 3;

        private readonly string[] weekSymbols = { "II", "I" };
        public IEnumerable<GroupSchedule> ExtractFromFile(string filePath)
        {

            List<GroupSchedule> schedules = new List<GroupSchedule>();

            ExcelPackage package;
            try
            {
                package = new ExcelPackage(new FileInfo(filePath));
            }
            catch
            {
                Console.WriteLine("Skipped - " + filePath);
                return new List<GroupSchedule>();
            }
            foreach (var sheet in package.Workbook.Worksheets)
            {
                schedules.AddRange(extractGroupSchedules(sheet));
            }
            Console.WriteLine("Extracted " + schedules.Count + " from file " + filePath);
            return schedules;
        }

        private List<GroupSchedule> extractGroupSchedules(ExcelWorksheet worksheet)
        {
            List<GroupSchedule> tableSchedules = new List<GroupSchedule>();
            int columnOffset = 0;
            if (worksheet.Cells[1, 2].Text.ToLower().Contains("осеннего"))
            {
                MireaDateHelper.SetSemester(SemesterSeason.AUTUMN);
            }
            else
            {
                MireaDateHelper.SetSemester(SemesterSeason.SPRING);
            }

            GroupSchedule generatedSchedule;
            while (!((generatedSchedule = extractGroupSchedule(worksheet, columnOffset, ref columnOffset)) is null))
            {
                tableSchedules.Add(generatedSchedule);
            }
            return tableSchedules;
        }

        private GroupSchedule extractGroupSchedule(ExcelWorksheet worksheet, int columnOffset, ref int newColumnOffset)
        {
            int newStartColumnOffset = -1;
            var cells = worksheet.Cells;
            for (int i = 1; i < groupSearchDepth + 1; i++)
            {
                if (groupBlockStartRegex.IsMatch(cells[groupNameRow, i + columnOffset].Text))
                {
                    newStartColumnOffset = i + columnOffset;
                    break;
                }
            }
            if (newStartColumnOffset == -1) return null;
            string groupName = cells[groupNameRow, newStartColumnOffset + disciplineColumnOffset].Text;
            var schedule = _scheduleFactory.CreateEmptySchedule(groupName);
            List<MireaLessonDescriber> scheduleDescribers = new List<MireaLessonDescriber>();
            for (DayOfWeek j = DayOfWeek.Sunday; j <= DayOfWeek.Saturday; j++)
            {
                scheduleDescribers.AddRange(generateDayDescribers(worksheet, j, newStartColumnOffset));
            }
            schedule.SetDescribers(scheduleDescribers);
            newColumnOffset = newStartColumnOffset;
            return schedule;
        }

        private IEnumerable<MireaLessonDescriber> generateDayDescribers(ExcelWorksheet worksheet, DayOfWeek dayOfWeek, int columnOffset)
        {
            var cells = worksheet.Cells;
            List<MireaLessonDescriber> describers = new List<MireaLessonDescriber>();
            for (int weekSymbolIndex = 0; weekSymbolIndex < weekSymbols.Length; weekSymbolIndex++)
            {
                string weekSymbol = weekSymbols[weekSymbolIndex];
                for (int i = 0; i < lessonLimit; i++)
                {
                    int lessonRow = 1;
                    string currentLesson = "";
                    DayOfWeek? currentDayOfWeek = null;
                    string timeText = "";
                    for (; lessonRow < rowLimit + 1; lessonRow++)
                    {
                        DayOfWeek? testDayOfWeek = getDayOfWeekByName(cells[lessonRow, dayNameColumn].Text);
                        string testLesson = cells[lessonRow, lessonNumberColumn].Text;
                        string testTimeText = cells[lessonRow, startTimeColumn].Text;
                        if (testLesson != "") currentLesson = testLesson;
                        if (!(testDayOfWeek is null)) currentDayOfWeek = testDayOfWeek;
                        if (testTimeText != "") timeText = testTimeText;
                        if (currentLesson == (i + 1).ToString() &&
                        currentDayOfWeek == dayOfWeek &&
                        cells[lessonRow, weekNumberColumn].Text == weekSymbol) break;
                    }
                    if (lessonRow >= rowLimit) continue;
                    MireaRawCellText data = new MireaRawCellText(cells[lessonRow, columnOffset + disciplineColumnOffset].Text,
                        cells[lessonRow, columnOffset + lessonTypeColumnOffset].Text,
                        cells[lessonRow, columnOffset + teacherColumnOffset].Text,
                        cells[lessonRow, columnOffset + auditoryColumnOffset].Text);
                    MireaSubjectCellInfo info = _subjectExtractor.Extract(data);
                    if (!info.HasData) continue;
                    foreach (var singleInfo in info.Subjects)
                    {
                        List<int> activeWeeks = weekSymbolIndex % 2 == 0 ? MireaDateHelper.GetAllEvenWeekNumbers().ToList() : MireaDateHelper.GetAllOddWeekNumbers().ToList();
                        var extractedLesson = _lessonFactory.Create(singleInfo.SubjectText, singleInfo.AuditoryText, generateDateTime(timeText), defineLessonType(singleInfo.LessonTypeText));
                        if(!(singleInfo.ExcludedWeekNumbers is null))
                        {
                            activeWeeks = activeWeeks.Where(weekNumber => !singleInfo.ExcludedWeekNumbers.Contains(weekNumber)).ToList();
                        }
                        else if(!(singleInfo.IncludedWeekNumbers is null))
                        {
                            activeWeeks = singleInfo.IncludedWeekNumbers.ToList();
                        }
                        var extractedLessonDescriber = _lessonDescriberFactory.Create(extractedLesson, activeWeeks, dayOfWeek);
                        describers.Add(extractedLessonDescriber);
                    }
                }
            }
            return describers;
        }

        private static DateTime generateDateTime(string timeCellText)
        {
            var parts = timeCellText.Split('-');
            if (parts.Length < 2) throw new ArgumentException("Incorrect input string", nameof(timeCellText));
            int hours = int.Parse(parts[0]);
            int minutes = int.Parse(parts[1]);
            return new DateTime(1, 1, 1, hours, minutes, 0);
        }
        private static LessonType defineLessonType(string lessonTypeCellText)
        {
            string firstPart = lessonTypeCellText.Split()[0].Split('/')[0];
            switch (firstPart.ToUpper())
            {
                case "ЛБ":
                    return LessonType.LAB;
                case "ЛАБ":
                    return LessonType.LAB;
                case "Л":
                    return LessonType.LECTION;
                case "ЛК":
                    return LessonType.LECTION;
                case "ЛЕК":
                    return LessonType.LECTION;
                case "П":
                    return LessonType.PRACTIC;
                case "ПР":
                    return LessonType.PRACTIC;
                case "СР":
                    return LessonType.AUTONOMOUS_WORK;
                default:
                    return LessonType.UNDEFINED;
            }
        }
        private static DayOfWeek? getDayOfWeekByName(string dayName)
        {
            Dictionary<string, DayOfWeek> weekDaysAssociations = new Dictionary<string, DayOfWeek>()
            {
                {"понедельник", DayOfWeek.Monday },
                {"вторник", DayOfWeek.Tuesday },
                {"среда", DayOfWeek.Wednesday },
                {"четверг", DayOfWeek.Thursday },
                {"пятница", DayOfWeek.Friday },
                {"суббота", DayOfWeek.Saturday },
                {"воскресенье", DayOfWeek.Sunday },
            };
            if (!weekDaysAssociations.ContainsKey(dayName.ToLower())) return null;
            return weekDaysAssociations[dayName.ToLower()];
        }
    }

    internal class MireaSubjectExtractor
    {
        private readonly string[] _restrictedPatterns = { @"\.\.\.\.\.", @"………", @"(.*военная.*)", @"(.*Военная.*)" };
        private readonly string excludedWeeksPattern = @"кр(\.?)(\s?)(?<weekNumbers>\d.*)(\s?)н\. (?<subjectName>.*)";
        private readonly string includedWeeksPattern = @"(?<weekNumbers>\d.*)(\s?)н\. (?<subjectName>.*)";
        private readonly string onlySubjectNamePattern = @"(?<subjectName>.*)";
        private readonly string subgroupPattern = @"\d(\s?)п([/]?)г";
        public MireaSubjectCellInfo Extract(MireaRawCellText rawText)
        {
            MireaSubjectCellInfo info = new MireaSubjectCellInfo();
            if (!checkCellText(rawText.DicsiplineCellText)) return info;
            string[] disciplineCellTextParts = rawText.DicsiplineCellText.Replace("   ", "\n").Split('\n').Where(part => !string.IsNullOrEmpty(part.Trim())).ToArray();
            disciplineCellTextParts = findAndConcatSubgroups(disciplineCellTextParts);
            string[] lessonTypeCellTextParts = rawText.LessonTypeCellText.Replace("   ", "\n").Split('\n').Where(part => !string.IsNullOrEmpty(part.Trim())).ToArray();
            string[] teacherCellTextParts = rawText.TeacherCellText.Replace("   ", "\n").Split('\n').Where(part => !string.IsNullOrEmpty(part.Trim())).ToArray();
            string[] auditoryCellTextParts = rawText.AuditoryCellText.Replace("   ", "\n").Split('\n').Where(part => !string.IsNullOrEmpty(part.Trim())).ToArray();
            for (int i = 0; i < disciplineCellTextParts.Length; i++)
            {
                if (string.IsNullOrEmpty(disciplineCellTextParts[i])) continue;
                string disciplinePart = disciplineCellTextParts[i];
                string lessonTypePart = lessonTypeCellTextParts.Length > 0 ? i < lessonTypeCellTextParts.Length ? lessonTypeCellTextParts[i] : lessonTypeCellTextParts[0] : "";
                string teacherPart = teacherCellTextParts.Length > 0 ? i < teacherCellTextParts.Length ? teacherCellTextParts[i] : teacherCellTextParts[0] : "";
                string auditoryPart = auditoryCellTextParts.Length > 0 ? i < auditoryCellTextParts.Length ? auditoryCellTextParts[i] : auditoryCellTextParts[0] : "";
                string[] singleParts = { disciplinePart.Trim(), lessonTypePart.Trim(), teacherPart.Trim(), auditoryPart.Trim() };
                var extractedSingle = extractSingle(singleParts);
                if (extractedSingle is null) continue;
                info.AddSingleSubjectInfo(extractedSingle);
            }
            return info;
        }

        private string[] findAndConcatSubgroups(string[] parts)
        {
            List<string> disciplineCellTextPreprocessParts = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i < parts.Length - 1 && !checkDefinedSubgroupNumbers(parts[i]) && checkDefinedSubgroupNumbers(parts[i + 1]) && !checkDefinedWeekNumbers(parts[i+1]))
                {
                    disciplineCellTextPreprocessParts.Add(parts[i] + " " + parts[i + 1]);
                    i++;
                }
                else
                {
                    disciplineCellTextPreprocessParts.Add(parts[i]);
                }
            }
            return disciplineCellTextPreprocessParts.ToArray();
        }

        private bool checkDefinedSubgroupNumbers(string inputPart)
        {
            Regex subGroupRegex = new Regex(subgroupPattern);
            return subGroupRegex.IsMatch(inputPart);
        }

        private bool checkDefinedWeekNumbers(string inputPart)
        {
            return new Regex(excludedWeeksPattern).IsMatch(inputPart) || new Regex(includedWeeksPattern).IsMatch(inputPart);
        }

        private bool checkCellText(string disciplineCellText)
        {
            if (string.IsNullOrEmpty(disciplineCellText)) return false;
            foreach(var pattern in _restrictedPatterns)
            {
                Regex regex = new Regex(pattern);
                if (regex.IsMatch(disciplineCellText)) return false;
            }
            return true;
        }

        private MireaSingleSubjectInfo extractSingle(string[] cellTextPartArray)
        {
            List<int> excludedWeekNumbers = null;
            List<int> includedWeekNumbers = null;
            string subjectName;
            Match excludedWeeksMatch = new Regex(excludedWeeksPattern).Match(cellTextPartArray[0]);
            if(excludedWeeksMatch.Success)
            {
                excludedWeekNumbers = defineNumbers(preprocessWeekNumbersExpression(excludedWeeksMatch.Groups["weekNumbers"].Value));
                subjectName = excludedWeeksMatch.Groups["subjectName"].Value;
            }
            else
            {
                Match includedWeeksMatch = new Regex(includedWeeksPattern).Match(cellTextPartArray[0]);
                if (includedWeeksMatch.Success)
                {
                    includedWeekNumbers = defineNumbers(preprocessWeekNumbersExpression(includedWeeksMatch.Groups["weekNumbers"].Value));
                    subjectName = includedWeeksMatch.Groups["subjectName"].Value;
                }
                else
                {
                    Match onlySubjectNameMatch = new Regex(onlySubjectNamePattern).Match(cellTextPartArray[0]);
                    if(onlySubjectNameMatch.Success)
                    {
                        subjectName = onlySubjectNameMatch.Groups["subjectName"].Value;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            return new MireaSingleSubjectInfo(subjectName, cellTextPartArray[2], cellTextPartArray[3], cellTextPartArray[1], includedWeekNumbers, excludedWeekNumbers);
        }

        private static string preprocessWeekNumbersExpression(string expression)
        {
            Regex commaRegex = new Regex(@",,*");
            Regex dotRegex = new Regex(@"\.");
            Regex dashRegex = new Regex(@"\s?-\s?");
            string processedExpression = expression;
            processedExpression = dotRegex.Replace(processedExpression, ",");
            processedExpression = dashRegex.Replace(processedExpression, "-");
            processedExpression = commaRegex.Replace(processedExpression, ",");
            processedExpression = processedExpression.Replace(" ", ",");
            return processedExpression;
        }

        private static List<int> defineNumbers(string expression)
        {
            List<int> result = new List<int>();
            int temp;
            if (string.IsNullOrWhiteSpace(expression)) return new List<int>();
            if (int.TryParse(expression, out temp))
            {
                result.Add(temp);
                return result;
            }
            if (expression.Contains(","))
            {
                foreach (var expPart in expression.Split(','))
                {
                    result.AddRange(defineNumbers(expPart.Trim()));
                }
                return result;
            }
            int startNum = int.Parse(expression.Split('-')[0]);
            int endNum = int.Parse(expression.Split('-')[1]);
            for (int i = startNum; i < endNum + 1; i++)
            {
                result.Add(i);
            }
            return result;
        }

        

    }

    internal class MireaRawCellText
    {
        private string _dicsiplineCellText;
        private string _lessonTypeCellText;
        private string _teacherCellText;
        private string _auditoryCellText;

        public MireaRawCellText(string dicsiplineCellText, string lessonTypeCellText, string teacherCellText, string auditoryCellText)
        {
            _dicsiplineCellText = dicsiplineCellText;
            _lessonTypeCellText = lessonTypeCellText;
            _teacherCellText = teacherCellText;
            _auditoryCellText = auditoryCellText;
        }

        public string DicsiplineCellText => _dicsiplineCellText;
        public string LessonTypeCellText => _lessonTypeCellText;
        public string TeacherCellText => _teacherCellText;
        public string AuditoryCellText => _auditoryCellText;
    }

    internal class MireaSubjectCellInfo
    {
        private List<MireaSingleSubjectInfo> _subjects;
        public MireaSubjectCellInfo()
        {
            _subjects = new List<MireaSingleSubjectInfo>();
        }
        public bool HasData => _subjects.Count > 0;
        public IEnumerable<MireaSingleSubjectInfo> Subjects => _subjects;
        public void AddSingleSubjectInfo(MireaSingleSubjectInfo info) => _subjects.Add(info);
    }

    internal class MireaSingleSubjectInfo
    {
        private string _subjectText;
        private string _teacherText;
        private string _auditoryText;
        private string _lessonTypeText;
        private IEnumerable<int> _excludedWeekNumbers;
        private IEnumerable<int> _includedWeekNumbers;
        public MireaSingleSubjectInfo(string subjectText, string teacherText, string auditoryText, string lessonTypeText, IEnumerable<int> includedWeekNumbers, IEnumerable<int> excludedWeekNumbers)
        {
            _subjectText = subjectText;
            _teacherText = teacherText;
            _auditoryText = auditoryText;
            _lessonTypeText = lessonTypeText;
            _includedWeekNumbers = includedWeekNumbers;
            _excludedWeekNumbers = excludedWeekNumbers;
        }
        public string SubjectText => _subjectText;
        public string TeacherText => _teacherText;
        public string AuditoryText => _auditoryText;
        public string LessonTypeText => _lessonTypeText;
        public IEnumerable<int> ExcludedWeekNumbers => _excludedWeekNumbers;
        public IEnumerable<int> IncludedWeekNumbers => _includedWeekNumbers;
    }

    enum SemesterSeason
    {
        AUTUMN = 0,
        SPRING = 1,
    }

    internal static class MireaDateHelper
    {
        private static DateTime semesterStartDate;
        private static DateTime semesterEndDate;

        public static DateTime SemesterStartDate => semesterStartDate;
        public static DateTime SemesterEndDate => semesterEndDate;

        private static DateTime semesterFirstWeekDate
        {
            get => semesterStartDate - new TimeSpan(((int)semesterStartDate.DayOfWeek - 2) % 7, 0, 0, 0);
        }
        public static void SetSemester(SemesterSeason season)
        {
            switch (season)
            {
                case SemesterSeason.AUTUMN:
                    semesterStartDate = new DateTime(DateTime.Now.Year, 9, 1);
                    semesterEndDate = new DateTime(DateTime.Now.Year, 12, 31);
                    break;
                case SemesterSeason.SPRING:
                    semesterStartDate = new DateTime(DateTime.Now.Year, 2, 1);
                    semesterEndDate = new DateTime(DateTime.Now.Year, 5, 31);
                    break;
                default:
                    throw new ArgumentException("Unexpected enum argument value", nameof(season));
            }
        }

        public static int GetWeekNumberOfDay(DateTime dayDate)
        {
            var difference = dayDate - semesterFirstWeekDate;
            return (int)Math.Ceiling(difference.Days / 7.0);
        }

        public static IEnumerable<int> GetAllOddWeekNumbers()
        {
            List<int> numbers = new List<int>();
            for (int i = 0; i < (semesterEndDate - semesterFirstWeekDate).TotalDays / 7; i += 2)
            {
                numbers.Add(i + 1);
            }
            return numbers;
        }

        public static IEnumerable<int> GetAllEvenWeekNumbers()
        {
            List<int> numbers = new List<int>();
            for (int i = 1; i < (semesterEndDate - semesterFirstWeekDate).TotalDays / 7; i += 2)
            {
                numbers.Add(i + 1);
            }
            return numbers;
        }
    }

    internal static class HTTPClient
    {
        public static string GetString(string url)
        {
            var response = sendRequest(url);
            if (!checkStatusCode(response.StatusCode)) return null;
            var result = response.Content.ReadAsStringAsync().Result;
            return result;
        }

        public static byte[] GetBytes(string url)
        {
            var response = sendRequest(url);
            if (!checkStatusCode(response.StatusCode)) return null;
            var result = response.Content.ReadAsByteArrayAsync().Result;
            return result;
        }

        private static HttpResponseMessage sendRequest(string url)
        {
            var client = new HttpClient();
            var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)).Result;
            return response;
        }

        private static bool checkStatusCode(HttpStatusCode code)
        {
            return code == HttpStatusCode.OK;
        }
    }

    internal static class HTMLHelper
    {
        public static string[] GetAllAttributeValues(string html, string attributeName)
        {
            List<string> allValues = new List<string>();
            string regularExp = $"{attributeName}=\"(?<rawAttribute>.*)\"";
            var matches = Regex.Matches(html, regularExp);
            foreach (Match match in matches)
            {
                string value = match.Groups["rawAttribute"].Value;
                string url = value.Split('"')[0];
                allValues.Add(url);
            }
            return allValues.ToArray();
        }
    }
}