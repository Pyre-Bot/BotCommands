using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using RoR2;
using RoR2.Stats;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Console = RoR2.Console;
using Debug = UnityEngine.Debug;
using Path = System.IO.Path;


namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.3.0")]
    public class BotCommands : BaseUnityPlugin
    {
        // Config
        private static ConfigEntry<string> Cmdpath { get; set; }
        private string _botcmdPath;
        private static ConfigEntry<string> Servername { get; set; }
        private static string _serverName;
        // Create custom log source
        private static ManualLogSource Log = new ManualLogSource("BotCommands");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Awake()
        {
            // Register custom log source
            BepInEx.Logging.Logger.Sources.Add(Log);
            
            // Path is the current path of the .DLL
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Cmdpath = Config.Bind<string>(
                "Config",
                "botcmd",
                path + @"\botcmd.txt",
                "Insert the path of your botcmd.txt"
            );
            _botcmdPath = Cmdpath.Value;
            Servername = Config.Bind<string>(
                "Config",
                "Server Name",
                "Server1",
                "Enter the name of your server for stats tracking"
            );
            _serverName = Servername.Value;

            Log.LogInfo("Created by Rayss and InfernalPlacebo.");
            #if DEBUG
            Log.LogWarning("You are using a debug build!");
            #endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Start()
        {
            StartHooks();
            Reading();
            OpenExe();
        }

        private async void Reading()
        {
            // Create botcmd.txt if it doesn't exist yet
            using (StreamWriter w = File.AppendText(_botcmdPath))
            
            using (StreamReader reader = new StreamReader(new FileStream(_botcmdPath,
                     FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                long lastMaxOffset = reader.BaseStream.Length;

                while (true)
                {
                    await Task.Delay(1000);

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Exception handling, I guess
                        try
                        {
                            RoR2.Console.instance.SubmitCmd(null, line);
                        }
                        catch
                        {
                            Log.LogWarning("No sir, partner.");
                        }
                    }

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;
                }
            }
        }

        private static void StartHooks()
        {
            // On run end
            // LogTime and LogStagesCleared won't be needed after stats are done
            On.RoR2.RunReport.Generate += (orig, run, resulttype) =>
            {
                RunReport valid = orig(run, resulttype); // Required if the hooked command has a return value

                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    GetStats(user);
                }

                LogTime();
                LogStagesCleared();
                return valid; // Required if the hooked command has a return value
            };

            // On player leave
            // NOTE: Ensure that if a player joins and leaves multiple times during a run, their stats aren't reset. Maybe cache the stats for each player in the run locally and only upload to the DB when the run ends
            // Alternatively, make it so stats are only logged if you complete a run (aka delete this hook)
            // LogTime and LogStagesCleared won't be needed after stats are done
            On.RoR2.Networking.GameNetworkManager.OnServerDisconnect += (orig, run, conn) =>
            {
                if (Run.instance)
                {
                    NetworkUser user = FindNetworkUserForConnectionServer(conn);
                    GetStats(user);

                    LogTime();
                    LogStagesCleared();
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

            // On player join
            // Will be removed on new stat tracking
            On.RoR2.Networking.GameNetworkManager.OnServerConnect += (orig, run, conn) =>
            {
                orig(run, conn);
                if (!Run.instance) return;
                LogTime();
                LogStagesCleared();
            };
        }

        // Will be removed on new stat tracking
        private static void LogTime()
        {
            if (!Run.instance)
            {
                throw new ConCommandException("No run is currently in progress.");
            }
            Debug.Log("Run time is " + Run.instance.GetRunStopwatch().ToString());
        }

        // Will be removed on new stat tracking
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
                "totalDistanceTraveled",
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
            // Debug.Log(string.Join("\n", array)); <<<<< Used for seeing all the stat options
            // Argument organization: serverName, ID, timeAlive, kills, deaths, goldCollected, distanceTraveled, itemsCollected, stagesCompleted, purchases
            // Use ProcessStartInfo class
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = path + @"\BotCommands_Dynamo.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = string.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}", _serverName, steamId,
                sendToDynamo["totalTimeAlive"], sendToDynamo["totalKills"], sendToDynamo["totalDeaths"],
                sendToDynamo["totalGoldCollected"], sendToDynamo["totalDistanceTraveled"],
                sendToDynamo["totalItemsCollected"], sendToDynamo["totalStagesCompleted"],
                sendToDynamo["totalPurchases"]);

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

        // Used for debugging database
        [Conditional("DEBUG")]
        private static void OpenExe()
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = path + @"\BotCommands_Dynamo.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = "server9 123456789 100 100 100 100 100 100 100 100";

            try
            {
                // Start the process with the info we specified.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    Log.LogWarning("BotCommands: Updating stats database!");
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