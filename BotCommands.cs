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
using R2API;
using R2API.Utils;
using Debug = UnityEngine.Debug;
using Path = System.IO.Path;
using Random = System.Random;


namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.3.0")]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    public partial class BotCommands : BaseUnityPlugin
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
            // Register commands with console
            R2API.Utils.CommandHelper.AddToConsoleWhenReady();
            
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
            
            // These are only called if built in debug mode
            OpenExe();
            RandomString();
        }

        private async void Reading()
        {
            // Create botcmd.txt if it doesn't exist yet
            using (StreamWriter w = File.AppendText(_botcmdPath))
            {
                w.Close();
            }
            
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
            
            // TODO: Finish this hook
            On.RoR2.Chat.CCSay += (orig, self) =>
            {
                string message = self[0];
                string sender = self.sender.gameObject.ToString();
                Log.LogWarning($"Found Message by {sender}: {message}");
                orig(self);
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
        
        // TODO: Finish command, currently generated code is sent to everyone
        // TODO: Need to add new database to Dynamo to store temporary auth codes and add a argument to BotCommands_Dynamo to know when we are sending an auth code
        // Link command
        [ConCommand(commandName = "Link", flags = ConVarFlags.ExecuteOnServer,
            helpText = "Generates a random code to link your account on Discord")]
        private static void LinkCommand(ConCommandArgs args)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var stringChars = new char[4];

            for (var i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = new string(stringChars);
            Chat.AddMessage("Your link code is " + finalString);
        }
        
        // TODO: Add logic here
        // Send chat messages to database
        private static void SendMessageToDB()
        {
            
        }
    }
}