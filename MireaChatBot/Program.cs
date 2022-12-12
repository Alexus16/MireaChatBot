﻿using MireaChatBot.Bot;
using MireaChatBot.BotHandlers;
using MireaChatBot.ChatHandlers;
using MireaChatBot.DataContainers;
using MireaChatBot.GroupContainers;
using MireaChatBot.ScheduleAccessors;
using MireaChatBot.ScheduleParsers;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;

namespace MireaChatBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            TelegramClient client = new TelegramClient(new TelegramBotData("", null, 0));
            GroupContainerBuilder builder = new GroupContainerBuilder(client);
            KeyTimeDataContainer keyTimeContainer = new KeyTimeDataContainer();
            keyTimeContainer.AddKeyTime("poll-send", DateTime.Parse("19:00"));
            //  Containers
            var customUserContainer = new CustomUserDataContainer();
            var accessor = new CacheAccessorWithoutDB(new MireaScheduleParser());
            var scheduleContainer = new ScheduleDataContainer(accessor);
            var parser = new MireaScheduleParser();
            builder.AddDataContainer(customUserContainer);
            builder.AddDataContainer(scheduleContainer);
            builder.AddDataContainer(keyTimeContainer);
            //  Handlers
            builder.AddHandler<MireaGroupRegistratorHandler>();
            //  Handler factories
            builder.AddHandlerFactory<PollVisitHandlerFactory>();
            builder.AddHandlerFactory<AttachmentHandlerFactory>();
            accessor.Update();
            builder.Client.StartWork();
            while (Console.ReadLine() != null)
            {
                var settinds = new JsonSerializerSettings();
                settinds.MaxDepth = 15;
            }
        }
    }
}
