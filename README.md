# BotCommands
Retrieves server information and allows sending commands to the game via third parties by adding commands to the botcmd.txt file. Also publishes stats to a database with the included BotCommands_Dynamo.exe file.

## Install
Download the latest release from GitHub and put the BotCommands.dll and BotCommands_Dynamo.exe in your BepInEx\Plugins folder.

Using the BotCommands_Dynamo.exe requires you to have an AWS account set up and the credentials stored on your computer or server already, the recommended way to do this is with [AWS CLI](https://aws.amazon.com/cli/).

Can be auto installed via the setup script in [Pyre Bot](https://github.com/InfernalPlacebo/pyre-bot).

## Build
If you want to build from source:
- Clone the repo to your computer
- Remove the dependencies and add new ones matching the path to the files on your computer
    - Recommendation: Create a folder in the root directory caled **lib**, if you add the dependencies there they will be picked up automatically.
- Install the following NuGet packages into the **BotCommands_Dynamo** project
    - AWSSDK.DynamoDBv2
        - `Install-Package AWSSDK.DynamoDBv2 -Version 3.3.105.33`
    - NETStandard.Library
        - `Install-Package NETStandard.Library -Version 2.0.3`
    - Newtonsoft.Json
        - `https://www.nuget.org/packages/Newtonsoft.Json`
 - Build BotCommands normally with your IDE
 - Build BotCommands_Dynamo with the `dotnet cli`
    - `dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true`
    - Self-Contained .exe file will be located: `repo_directory\bin\Release\netcoreapp3.1\win-x64\publish`

## Version History
### 0.3.0
 - Implemented stat tracking directly into the plugin!
    - BotCommands_Dynamo.exe is called from the plugin file when a player leaves the server to update the stats database
 - Significant code cleanup
 - Changed config file to use current directory as default location for botcmd.txt
    - botcmd.txt is created on server launch if it doesn't exist
### 0.2.0
 - We didn't take notes on this one, oops!
### 0.1.2
- Improved game responsiveness
### 0.1.1
- Improved game responsiveness when using BotCommands
### 0.1.0
- Initial commit
- Creates config file with variable used to define the file path of botcmd.txt
