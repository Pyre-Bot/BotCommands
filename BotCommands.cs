using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using RoR2;
using RoR2.Stats;
using UnityEngine.Networking;
using System.Threading.Tasks;


namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.2.0")]
    public class BotCommands : BaseUnityPlugin
    {
        // Config
        private static ConfigEntry<string> Cmdpath { get; set; }
        private string botcmd_path;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Awake()
        {
            Cmdpath = Config.Bind<string>(
            "Config",
            "botcmd",
                    "C:/Program Files (x86)/Steam/steamapps/common/Risk of Rain 2 Dedicated Server/BepInEx/plugins/botcmd.txt",
            "Insert the path of your botcmd.txt"
            );
            botcmd_path = Cmdpath.Value;

            Debug.Log("Created by Rayss and InfernalPlacebo.");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members")]
        private void Start()
        {
            StartHooks();
            Reading();
        }

        private async void Reading()
        {
            using (StreamReader reader = new StreamReader(new FileStream(botcmd_path,
                     FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                long lastMaxOffset = reader.BaseStream.Length;
                string line;

                while (true)
                {
                    await Task.Delay(1000);

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Exception handling, I guess
                        try
                        {
                            RoR2.Console.instance.SubmitCmd(null, line);
                        }
                        catch
                        {
                            Debug.Log("No sir, partner.");
                        }
                    }

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;
                }
            }
        }


        public static void StartHooks()
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
                if (Run.instance)
                {
                    LogTime();
                    LogStagesCleared();
                }
            };

            // On player join
            // Will be removed on new stat tracking
            On.RoR2.Networking.GameNetworkManager.OnServerConnect += (orig, run, conn) =>
            {
                orig(run, conn);
                if (Run.instance)
                {
                    LogTime();
                    LogStagesCleared();
                }
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
            StatSheet statSheet;
            PlayerStatsComponent component = playerMasterObject.GetComponent<PlayerStatsComponent>();
            statSheet = (component?.currentStats);
            // Print the statsheet to console / log
            // Will be changing this to parse and add to the database
            // Don't need all the stats they access though, should only use some of the fields (may be able to split up by category)
            string[] array = new string[statSheet.fields.Length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = string.Format("[\"{0}\"]={1}", statSheet.fields[i].name, statSheet.fields[i].ToString());
            }
            // Literally all I have to do is parse the array to be used by the db
            Debug.Log(string.Join("\n", array));
        }

        // Borrowed from R2DSEssentials.Util.Networking
        private static NetworkUser FindNetworkUserForConnectionServer(NetworkConnection connection)
        {
            foreach (var networkUser in NetworkUser.readOnlyInstancesList)
            {
                if (networkUser.connectionToClient == connection)
                {
                    return networkUser;
                }
            }

            return null;
        }
    }
}