﻿using BepInEx;
using BepInEx.Configuration;
using System.IO;
using UnityEngine;

namespace BotCMDs
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Rayss.BotCommands", "BotCommands", "0.1.1")]
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
        private void Update()
        {
            using (StreamReader reader = new StreamReader(new FileStream(botcmd_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                long lastMaxOffset = reader.BaseStream.Length;

                System.Threading.Thread.Sleep(4);

                //seek to the last max offset
                reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                //read out of the file until the EOF
                string line = "";

                while ((line = reader.ReadLine()) != null)
                    RoR2.Console.instance.SubmitCmd(null, line);
            }
        }
    }
}