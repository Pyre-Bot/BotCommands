using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using On.RoR2.Networking;
using R2API.Utils;
using RoR2;
using RoR2.Stats;
using UnityEngine.Networking;
using Chat = On.RoR2.Chat;
using Console = RoR2.Console;
using Path = System.IO.Path;
using RunReport = On.RoR2.RunReport;

namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.3.0")]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    public class BotCommands : BaseUnityPlugin
    {
        private static string _serverName;

        // Create custom log source
        private static readonly ManualLogSource Log = new ManualLogSource("BotCommands");

        private string _botcmdPath;

        // Config
        private static ConfigEntry<string> Cmdpath { get; set; }
        private static ConfigEntry<string> Servername { get; set; }

        [SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Awake()
        {
            // Register custom log source
            BepInEx.Logging.Logger.Sources.Add(Log);
            // Register commands with console
            R2API.Utils.CommandHelper.AddToConsoleWhenReady();

            // Path is the current path of the .DLL
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Cmdpath = Config.Bind(
                "Config",
                "botcmd",
                path + @"\botcmd.txt",
                "Insert the path of your botcmd.txt"
            );
            _botcmdPath = Cmdpath.Value;
            Servername = Config.Bind(
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

        [SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Start()
        {
            StartHooks();
            Reading();
        }

        private async void Reading()
        {
            // Create botcmd.txt if it doesn't exist yet
            using (var w = File.AppendText(_botcmdPath))
            {
                w.Close();
            }

            using (var reader = new StreamReader(new FileStream(_botcmdPath,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                var lastMaxOffset = reader.BaseStream.Length;

                while (true)
                {
                    await Task.Delay(1000);

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        // Exception handling, I guess
                        try
                        {
                            Console.instance.SubmitCmd(null, line);
                        }
                        catch
                        {
                            Log.LogWarning("No sir, partner.");
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
            RunReport.Generate += (orig, run, resulttype) =>
            {
                var valid = orig(run, resulttype); // Required if the hooked command has a return value

                foreach (var user in NetworkUser.readOnlyInstancesList)
                    new Thread(delegate() { GetStats(user); }).Start();
                return valid; // Required if the hooked command has a return value
            };

            // On player leave
            // LogTime and LogStagesCleared won't be needed after stats are done
            GameNetworkManager.OnServerDisconnect += (orig, run, conn) =>
            {
                if (Run.instance)
                {
                    var user = FindNetworkUserForConnectionServer(conn);
                    new Thread(delegate() { GetStats(user); }).Start();
                }

                orig(run, conn);
            };
        }

        private static void GetStats(NetworkUser user)
        {
            // Unity variables
            var playerMasterObject = user.masterObject;
            var steamId = Convert.ToInt64(user.id.steamId.ToString());
            StatSheet statSheet;
            var component = playerMasterObject.GetComponent<PlayerStatsComponent>();
            statSheet = component?.currentStats;
            var sendToDynamo = new Dictionary<string, string>();
            // Creates the array of the run report
            var array = new string[statSheet.fields.Length];
            // Iterates through the run report to add to a dictionary
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = string.Format("[\"{0}\"]={1}", statSheet.fields[i].name, statSheet.fields[i].ToString());
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
            var startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = path + @"\BotCommands_Dynamo.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = result;
            Log.LogError(result);
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

        // Borrowed from R2DSEssentials.Util.Networking
        private static NetworkUser FindNetworkUserForConnectionServer(NetworkConnection connection)
        {
            return NetworkUser.readOnlyInstancesList.FirstOrDefault(networkUser =>
                networkUser.connectionToClient == connection);
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

            for (var i = 0; i < stringChars.Length; i++) stringChars[i] = chars[random.Next(chars.Length)];

            var finalString = new string(stringChars);
            RoR2.Chat.AddMessage("Your link code is " + finalString);
        }
    }
}