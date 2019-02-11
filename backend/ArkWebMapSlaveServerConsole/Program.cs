﻿using ArkWebMapSlaveServer;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Http;
using ArkBridgeSharedEntities.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using ArkBridgeSharedEntities.Entities.RemoteConfig;

namespace ArkWebMapSlaveServerConsole
{
    /// <summary>
    /// Nothing but a frontend console for the code.
    /// </summary>
    partial class Program
    {
        const string CONFIG_FILE_NAME = "config_net.json";
        const int SETUP_VERSION = 1;
        const float CURRENT_RELEASE_ID = 1.1f;

        static RemoteConfigFile remote_config;

        static void Main(string[] args)
        {
            //Request the remote config file
            Console.WriteLine("Downloading remote configuration file...");
            try
            {
                using (WebClient wc = new WebClient())
                {
                    remote_config = JsonConvert.DeserializeObject<RemoteConfigFile>(wc.DownloadString("https://ark.romanport.com/client_config.json"));
                }
            } catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to download remote config file. Please try again later.");
                Console.ReadLine();
                return;
            }

            //If we are out of date, display it
            if(CURRENT_RELEASE_ID < remote_config.sub_server_config.minimum_release_id)
            {
                Console.Clear();
                RemoteConfigFile_Release latest_release = remote_config.latest_release;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("This version of ArkWebMap is out of date!");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n" + latest_release.release_notes+"\nMore info: "+latest_release.download_page);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nPress enter to automatically download and install the new version.");
                Console.ReadLine();

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.White;
                UpdateInstaller.InstallUpdate(latest_release);

                return;
            }
            
            //If the config file exists, jump right to running it.
            if (File.Exists(CONFIG_FILE_NAME))
            {
                Run();
            }
            else
            {
                //Not set up. Show prompt and ask for info
                IntroAndPromptUserForCode();

                //Now that we have the code, we can start to do setup. 
                //Send ready
                SendMasterMessage(new ArkSetupProxyMessage
                {
                    data = new Dictionary<string, string>
                    {
                        {"setup_version",SETUP_VERSION.ToString() }
                    },
                    type = ArkSetupProxyMessage_Type.ServerHello
                });

                //Set text
                Console.Clear();
                DrawWithColor("You're almost ready to go! ", ConsoleColor.Blue);
                DrawWithColor("Head back into your web browser to finish configuring the server.\n");

                //Begin checking for events and responding to them.
                bool continueLoop = true;
                while(continueLoop)
                {
                    List<ArkSetupProxyMessage> messages = GetMasterMessages();
                    foreach (ArkSetupProxyMessage message in messages)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(message));
                        switch (message.type)
                        {
                            case ArkSetupProxyMessage_Type.WebClientRequestServerTestPort:
                                TcpTester.OnBeginRequest(message);
                                break;
                            case ArkSetupProxyMessage_Type.CheckArkFile:
                                MapFileTester.OnBeginRequest(message);
                                break;
                            case ArkSetupProxyMessage_Type.UploadConfigAndFinish:
                                //Deserialize and save
                                File.WriteAllText(GetConfigPath(), message.data["config"]);

                                //Respond
                                SendMasterMessage(new ArkSetupProxyMessage
                                {
                                    data = new Dictionary<string, string>(),
                                    type = ArkSetupProxyMessage_Type.ServerGoodbye
                                });
                                continueLoop = false;
                                break;
                        }
                    }
                    Thread.Sleep(4000);
                }
            }
            Console.Clear();
            DrawWithColor("Done! Starting as usual now...", ConsoleColor.Blue);
            Console.ForegroundColor = ConsoleColor.White;
            Run();
        }



        

        static string clientSessionId;

        static List<ArkSetupProxyMessage> GetMasterMessages()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(GetProxyEndpoint());
                request.Method = "GET";
                var response = (HttpWebResponse)request.GetResponse();
                string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                return JsonConvert.DeserializeObject<List<ArkSetupProxyMessage>>(responseString); 
            } catch
            {
                return new List<ArkSetupProxyMessage>();
            }
        }

        public static void SendMasterMessage(ArkSetupProxyMessage message)
        {
            string ser_string = JsonConvert.SerializeObject(message);
            Console.WriteLine("SENT " + ser_string);
            byte[] ser = Encoding.UTF8.GetBytes(ser_string);
            var request = (HttpWebRequest)WebRequest.Create(GetProxyEndpoint());

            request.Method = "POST";
            request.ContentLength = ser.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(ser, 0, ser.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            if(response.StatusCode != HttpStatusCode.OK)
            {
                DrawWithColor("Failed to send master server message. Are you online? ", ConsoleColor.Red);
                Console.ReadLine();
                throw new Exception();
            }
            //Assume response is OK if we did not get an error
        }

        public static string GetProxyEndpoint()
        {
            return remote_config.sub_server_config.endpoints.server_setup_proxy.Replace("{clientSessionId}", clientSessionId);
        }

        static string GetConfigPath()
        {
            return Directory.GetCurrentDirectory().TrimEnd('\\') + "\\" + CONFIG_FILE_NAME;
        }

        static void Run()
        {
            string config_path = GetConfigPath();
            ArkSlaveConfig config = JsonConvert.DeserializeObject<ArkSlaveConfig>(File.ReadAllText(config_path));
            Task t = ArkWebMapServer.MainAsync(config, config_path, remote_config);
            if(t != null)
                t.GetAwaiter().GetResult();
        }

        static void DrawWithColor(string message, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.Write(message);
        }
    }
}
