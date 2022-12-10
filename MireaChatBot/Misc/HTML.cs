using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MireaChatBot.Misc
{
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
