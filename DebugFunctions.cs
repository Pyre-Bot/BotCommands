using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;

namespace BotCMDs
{
    public partial class BotCommands : BaseUnityPlugin
    {
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
            startInfo.Arguments = "serverTest 123456789 100 100 100 100 100 100 100 100";

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

        // Outputs a random string on launch
        [Conditional("DEBUG")]
        private static void RandomString()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var stringChars = new char[8];

            for (var i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = new string(stringChars);
            Log.LogWarning(finalString);
        }
    }
}