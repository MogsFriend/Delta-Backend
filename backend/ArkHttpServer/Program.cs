﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ArkSaveEditor;
using ArkSaveEditor.World;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using ArkBridgeSharedEntities.Requests;
using LiteDB;

namespace ArkHttpServer
{
    public delegate void SendTribeNotification(int tribeId, TribeNotification n);
    public delegate object SendRequestToMaster(string action, object request, Type t);

    public partial class ArkWebServer
    {
        public static ServerConfigFile config;
        public static SendTribeNotification tribeNotificationCode;
        public static SendRequestToMaster sendRequestToMasterCode;

        public static System.Timers.Timer event_checker_timer;

        public static string api_prefix;

        public const int EVENTS_HEARTRATE = 10000; //How often event requests come in.

        public static LiteDatabase db;

        public static void Configure(ServerConfigFile config, string api_prefix, SendTribeNotification tribeNotificationCode, SendRequestToMaster sendToMasterCode, LiteDatabase db)
        {
            //Load
            ArkWebServer.config = config;
            ArkWebServer.api_prefix = api_prefix;
            ArkWebServer.tribeNotificationCode = tribeNotificationCode;
            ArkWebServer.sendRequestToMasterCode = sendToMasterCode;
            ArkWebServer.db = db;

            //Load save editor entries
            ArkImports.ImportContent(@"PrimalData/");

            //Load map
            WorldLoader.GetWorld();

            //Start event checker timer
            event_checker_timer = new System.Timers.Timer(5000);
            event_checker_timer.Elapsed += Event_checker_timer_Elapsed;
            event_checker_timer.Start();
        }

        public static bool CheckPermission(string name)
        {
            return config.base_permissions.CheckPermission(name);
        }

        public static void Log(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void Event_checker_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Check all of our sessions for updated files.
            HttpServices.EventService.AddEvent(new Entities.HttpSessionEvent(null, Entities.HttpSessionEventType.TestEvent));
            
            //Check for map updates
            if(WorldLoader.CheckForMapUpdates())
            {
                HttpServices.EventService.AddEvent(new Entities.HttpSessionEvent(null, Entities.HttpSessionEventType.MapUpdate));
            }

            //Find ALL baby dinos and check their stats so we can send notifications to mobile clients
            MobileNotificationsEngine.BabyDinoNotifications.CheckBabyDinos();
        }

        

        public static Task QuickWriteToDoc(Microsoft.AspNetCore.Http.HttpContext context, string content, string type = "text/html", int code = 200)
        {
            var response = context.Response;
            response.StatusCode = code;
            response.ContentType = type;

            //Load the template.
            string html = content;
            var data = Encoding.UTF8.GetBytes(html);
            response.ContentLength = data.Length;
            return response.Body.WriteAsync(data, 0, data.Length);
        }

        public static Task QuickWriteJsonToDoc<T>(Microsoft.AspNetCore.Http.HttpContext context, T data, int code = 200)
        {
            Formatting format = Formatting.None;
            if(context.Request.Query.ContainsKey("isDebug"))
            {
                if (context.Request.Query["isDebug"] == "true")
                    format = Formatting.Indented;
            }
            return QuickWriteToDoc(context, JsonConvert.SerializeObject(data, format), "application/json", code);
        }

        public static string GenerateRandomString(int length)
        {
            string output = "";
            char[] chars = "qwertyuiopasdfghjklzxcvbnm1234567890QWERTYUIOPASDFGHJKLZXCVBNM".ToCharArray();
            Random rand = new Random();
            for (int i = 0; i < length; i++)
            {
                output += chars[rand.Next(0, chars.Length)];
            }
            return output;
        }

        public static RequestHttpMethod FindRequestMethod(Microsoft.AspNetCore.Http.HttpContext context)
        {
            return Enum.Parse<RequestHttpMethod>(context.Request.Method.ToLower());
        }

        public static T DecodePostBody<T>(Microsoft.AspNetCore.Http.HttpContext context)
        {
            string buf;
            using (StreamReader sr = new StreamReader(context.Request.Body))
                buf = sr.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(buf);
        }
    }

    public enum RequestHttpMethod
    {
        get,
        post,
        put,
        delete,
        options
    }
}
