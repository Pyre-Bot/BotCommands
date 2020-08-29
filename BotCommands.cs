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
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]  // Makes it so that the client doesn't need to run this mod to connect to the server
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.4.0")]
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
            R2API.Utils.CommandHelper.AddToConsoleWhenReady();
            // Path is the current path of the .DLL
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);  // Unused?

            Servername = Config.Bind<string>(
                "Config",
                "Server Name",
                "Server1",
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
            SeqLogRead(_seqserver, _seqapi, _seqfilter);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Update()
        {
            if (Consolequeue.Count > 0)
            {
                var consoleUser = new RoR2.Console.CmdSender();
                RoR2.Console.instance.SubmitCmd(consoleUser, Consolequeue.Dequeue());
            }
        }

        private async void SeqLogRead(string server, string apiKey, string filter)
        {
#if DEBUG
            Log.LogWarning(filter);
#endif
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
                    string command = evt.RenderMessage();
                    command = command.Trim(new char[] {'"'});
                    Consolequeue.Enqueue(command);
                });

            await stream;
        }
        private static void StartHooks()
        {
            // On run end
            On.RoR2.RunReport.Generate += (orig, run, resulttype) =>
            {
                RunReport valid = orig(run, resulttype);

                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    GetStats(user);
                }

                return valid;
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
            try  // Done to prevent users with no masterobject (spectators) from causing a NullReferenceException
            {
                // Unity variables
                var playerMasterObject = user.masterObject; 
                var steamId = Convert.ToInt64(user.id.steamId.ToString());
                var component = playerMasterObject.GetComponent<PlayerStatsComponent>();
                var statSheet = component?.currentStats;
                var sendToDynamo = new Dictionary<string, string>();
                // Creates the array of the run report
                var array = new string[statSheet.fields.Length];
                // Iterates through the run report to add to a dictionary
                for (var i = 0; i < array.Length; i++)
                {
                    array[i] = $"[\"{statSheet.fields[i].name}\"]={statSheet.fields[i].ToString()}";
                    if (statSheet.fields[i].ToString() == "0")
                    {
                    }
                    else
                    {
                        sendToDynamo[statSheet.fields[i].name] = statSheet.fields[i].ToString();
                    }
                }

                // Adds server and SteamID to dictionary
                sendToDynamo["Server"] = _serverName;
                sendToDynamo["SteamID"] = steamId.ToString();
                // Splits the dictionary into a string that can be used as an argument
                var result = string.Join(" ", sendToDynamo.Select(kvp => $"{kvp.Key},{kvp.Value}"));
                // Use ProcessStartInfo class
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    #if DEBUG
                var startInfo = new ProcessStartInfo
                {
                    CreateNoWindow = false,
                    UseShellExecute = true,
                    FileName = path + @"\BotCommands_Dynamo.exe",
                    WindowStyle = ProcessWindowStyle.Normal,
                    Arguments = result
                };
    #else
                var startInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = path + @"\BotCommands_Dynamo.exe",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Arguments = result
                };
    #endif
                try
                {
                    // Start the process with the info we specified.
                    using (var exeProcess = Process.Start(startInfo))
                    {
                        Log.LogInfo("BotCommands: Updating stats database!");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"BotCommands: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"BotCommands: {ex.Message}");
            }
        }

        // Borrowed from R2DSEssentials.Util.Networking
        private static NetworkUser FindNetworkUserForConnectionServer(NetworkConnection connection)
        {
            return NetworkUser.readOnlyInstancesList.FirstOrDefault(networkUser =>
                networkUser.connectionToClient == connection);
        }
        
    }
}