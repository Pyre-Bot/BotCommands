using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using RoR2;
using RoR2.Stats;
using UnityEngine.Networking;
using R2API.Utils;
using Debug = UnityEngine.Debug;
using Path = System.IO.Path;

using Seq.Api;
using Newtonsoft.Json.Linq;

using Serilog.Formatting.Compact.Reader;
using System.Reactive.Linq;

namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.3.0")]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    public partial class BotCommands : BaseUnityPlugin
    {
        // Config
        private static ConfigEntry<string> Servername { get; set; }
        private static string _serverName;

        private static ConfigEntry<string> Seqserver { get; set; }
        private static string _seqserver;

        private static ConfigEntry<string> Seqapi { get; set; }
        private static string _seqapi;

        private static ConfigEntry<string> Seqfilter { get; set; }
        private static string _seqfilter;

        // Create custom log source
        private static ManualLogSource Log = new ManualLogSource("BotCommands");

        Queue<string> Consolequeue = new Queue<string>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Awake()
        {
            // Register custom log source
            BepInEx.Logging.Logger.Sources.Add(Log);
            // Register commands with console
            CommandHelper.AddToConsoleWhenReady();
            // Path is the current path of the .DLL
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);  // Unused?

            Servername = Config.Bind<string>(
                "Config",
                "Server Name",
                "ServerTest",
                "Enter the name of your server for stats tracking"
            );
            _serverName = Servername.Value;

            Seqserver = Config.Bind<string>(
                "Config",
                "Seq Server Address",
                "http://example.com",
                "Enter the value of your Seq.net server address"
            );
            _seqserver = Seqserver.Value;

            Seqapi = Config.Bind<string>(
                "Config",
                "Seq API Key",
                "",
                "Enter the value of your Seq.net API Key"
            );
            _seqapi = Seqapi.Value;

            Seqfilter = Config.Bind<string>(
                "Config",
                "Seq Filter",
                "Contains(Channel,'670373469845979136') or Contains(Channel, '665998238171660320')", // Default uses two channels, which can be used as Admin + Regular command channels
                "Enter the value of your Seq filter"
            );
            _seqfilter = Seqfilter.Value;

            Log.LogInfo("Created by Rayss and InfernalPlacebo.");
#if DEBUG
            Log.LogWarning("You are using a debug build!");
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Start()
        {
            StartHooks();
            SeqLogRead(_seqserver, _seqapi, _seqfilter); // Having the filter to a plain value seems to give me a WebSocketException
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Update()
        {
            if (Consolequeue.Count > 0)
            {
                RoR2.Console.instance.SubmitCmd(null, Consolequeue.Dequeue());
            }
        }

        // TODO: Add the option for multiple filter values, so we can use admin and commands channels
        private async void SeqLogRead(string server, string apiKey, string filter)
        {
            Log.LogWarning(filter);
            var connection = new SeqConnection(server, apiKey);

            string strict = null;
            if (filter != null)
            {
                var converted = await connection.Expressions.ToStrictAsync(filter);
                strict = converted.StrictExpression;
            }

            var stream = await connection.Events.StreamAsync<JObject>(filter: strict);

            stream.Select(jObject => LogEventReader.ReadFromJObject(jObject))
                .Subscribe(evt => {
                    //Log.LogWarning(evt.RenderMessage());
                    string command = evt.RenderMessage();
                    command = command.Trim(new char[] {'"'});
                    //Log.LogWarning(command);
                    Consolequeue.Enqueue(command);
                });

            await stream;
        }

        //Log.LogWarning(evt)
        //RoR2.Console.instance.SubmitCmd(null, "say ahh")
        //string[] cmdargs = {"ahh"};
        //RoR2.Console.instance.RunClientCmd(null, "say", cmdargs);
        //Consolequeue.Enqueue("say ahh");

        private static void StartHooks()
        {
            // On run end
            On.RoR2.RunReport.Generate += (orig, run, resulttype) =>
            {
                RunReport valid = orig(run, resulttype); // Required if the hooked command has a return value

                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    GetStats(user);
                }

                return valid; // Required if the hooked command has a return value
            };

            // On player leave
            On.RoR2.Networking.GameNetworkManager.OnServerDisconnect += (orig, run, conn) =>
            {
                if (Run.instance)
                {
                    NetworkUser user = FindNetworkUserForConnectionServer(conn);
                    GetStats(user);
                }
                orig(run, conn);
            };

            // On scene change (unloaded, new scene not yet loaded)
            On.RoR2.FadeToBlackManager.OnSceneUnloaded += (orig, run) =>
            {
                orig(run);
                if (!Run.instance) return;
                LogTime();
                LogStagesCleared();
            };

        }

        private static void LogTime()
        {
            if (!Run.instance)
            {
                throw new ConCommandException("No run is currently in progress.");
            }
            Debug.Log("Run time is " + Run.instance.GetRunStopwatch().ToString());
        }

        private static void LogStagesCleared()
        {
            if (!Run.instance)
            {
                throw new ConCommandException("No run is currently in progress.");
            }
            Debug.Log("Stages cleared: " + Run.instance.NetworkstageClearCount.ToString());
        }

        private static void GetStats(NetworkUser user)
        {
            GameObject playerMasterObject = user.masterObject;
            long steamId = System.Convert.ToInt64(user.id.steamId.ToString());
            StatSheet statSheet;
            PlayerStatsComponent component = playerMasterObject.GetComponent<PlayerStatsComponent>();
            statSheet = (component?.currentStats);
            List<string> listOfStatNames = new List<string>
            {
                "totalTimeAlive",
                "totalKills",
                "totalDeaths",
                "totalGoldCollected",
                "totalItemsCollected",
                "totalStagesCompleted",
                "totalPurchases"
            };
            Dictionary<string, string> outputStats = new Dictionary<string, string>();
            Dictionary<string, string> sendToDynamo = new Dictionary<string, string>();
            // Don't need all the stats they access though, should only use some of the fields (may be able to split up by category)
            string[] array = new string[statSheet.fields.Length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = string.Format("[\"{0}\"]={1}", statSheet.fields[i].name, statSheet.fields[i].ToString());
                outputStats[statSheet.fields[i].name] = statSheet.fields[i].ToString();
            }
            foreach (var kvp in outputStats.Where(kvp => listOfStatNames.Contains(kvp.Key)))
            {
                sendToDynamo[kvp.Key] = kvp.Value;
            }
            #if DEBUG
            foreach (KeyValuePair<string, string> kvp in sendToDynamo)
            {
                Log.LogWarning(kvp.Key + " : " + kvp.Value);
            }
            #endif
            // Debug.Log(string.Join("\n", array)); <<<<< Used for seeing all the stat options
            // Argument organization: serverName, ID, timeAlive, kills, deaths, goldCollected, itemsCollected, stagesCompleted, purchases
            // Use ProcessStartInfo class
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = path + @"\BotCommands_Dynamo.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = string.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8}", _serverName, steamId,
                sendToDynamo["totalTimeAlive"], sendToDynamo["totalKills"], sendToDynamo["totalDeaths"],
                sendToDynamo["totalGoldCollected"], sendToDynamo["totalPurchases"],
                sendToDynamo["totalItemsCollected"], sendToDynamo["totalStagesCompleted"]);

            try
            {
                // Start the process with the info we specified.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    Log.LogInfo("BotCommands: Updating stats database!");
                }
            }
            catch
            {
                Log.LogError("BotCommands: Unable to find executable file!");
            }
        }
        
        // Borrowed from R2DSEssentials.Util.Networking
        private static NetworkUser FindNetworkUserForConnectionServer(NetworkConnection connection)
        {
            return NetworkUser.readOnlyInstancesList.FirstOrDefault(networkUser => networkUser.connectionToClient == connection);
        }
        
    }
}