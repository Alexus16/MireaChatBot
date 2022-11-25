using DocumentFormat.OpenXml.Drawing.Charts;
using MireaChatBot.Bot;
using MireaChatBot.ScheduleAccessors;
using MireaChatBot.ScheduleCreator;
using MireaChatBot.ScheduleParsers;
using MireaChatBot.ScheduleSender;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;

namespace MireaChatBot
{
    internal class Program
    {
        static ChatVisitorDataCollector collector;
        static void Main(string[] args)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            MireaScheduleParser parser = new MireaScheduleParser();
            GroupScheduleAccessor accessor = new CacheAccessorWithoutDB(parser);
            TelegramClient client = new TelegramClient(new TelegramBotData("5968371284:AAFu0yn8wLlAUHHM9rl0Ou5uFYVB6-L2NMI", null, 0));
            collector = new ChatVisitorDataCollector(client);
            client.MessageReceived += testConsoleOutput;
            client.StartWork();
            while(Console.ReadLine() != "")
            {
                Console.WriteLine("-----------------------------");
            }
            collector.Dispose();
            client.StopWork();
        }

        private static void testConsoleOutput(object sender, Message e)
        {
            var visitor = collector.GetVisitorById(e.From.Id);
            Console.WriteLine("Your fullname is " + visitor.FullName);
        }
    }
}
