using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace BotCommands_Dynamo
{
    public static partial class Dynamo
    {
        //DynamoDB variables
        private static AmazonDynamoDBClient client;
        private static TableDescription statsTableDescription;
        private static Table statsTable;
        private static readonly CancellationTokenSource source = new CancellationTokenSource();
        private static readonly CancellationToken token = source.Token;
        private static Document stats_record;

        //Used to tell if we errored and need to exit
        private static bool _progress;

        //Argument organization: serverName, ID, timeAlive, kills, deaths, goldCollected, distanceTraveled, itemsCollected, stagesCompleted, purchases
        public static void Main(string[] args)
        {
            if (args == null)
                return;
            var serverName = args[0];
            //Variables used for dictionary
            var ID = long.Parse(args[1]);
            var timeAlive = Convert.ToInt32(Math.Round(Convert.ToDouble(args[2])));
            var kills = Convert.ToInt32(Math.Round(Convert.ToDouble(args[3])));
            var deaths = Convert.ToInt32(Math.Round(Convert.ToDouble(args[4])));
            var goldCollected = Convert.ToInt32(Math.Round(Convert.ToDouble(args[5])));
            var distanceTraveled = Convert.ToInt32(Math.Round(Convert.ToDouble(args[6])));
            var itemsCollected = Convert.ToInt32(Math.Round(Convert.ToDouble(args[7])));
            var stagesCompleted = Convert.ToInt32(Math.Round(Convert.ToDouble(args[8])));
            var purchases = Convert.ToInt32(Math.Round(Convert.ToDouble(args[9])));

            //Variables used for DynamoDB table
            const string dbTableName = "BotCommands_Stats";
            var itemAttributes = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = "SteamID64",
                    AttributeType = "N"
                }
            };
            var keySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "SteamID64",
                    KeyType = "HASH"
                }
            };
            var tableProvisionedThroughput = new ProvisionedThroughput(5, 5);

            //Dictionary containing the stats
            var statsDictionary = new Dictionary<string, int>
            {
                {"totalTimeAlive", timeAlive},
                {"totalKills", kills},
                {"totalDeaths", deaths},
                {"totalGoldCollected", goldCollected},
                {"totalDistanceTraveled", distanceTraveled},
                {"totalItemsCollected", itemsCollected},
                {"totalStagesCompleted", stagesCompleted},
                {"totalPurchases", purchases}
            };
            //Turns dictionary into JSON
            var statsJson = JsonConvert.SerializeObject(statsDictionary);

            //Lets do things!
            Console.WriteLine(" -- Attempting to connect to the database --");
            //Create connection to the database
            //Progress variable is used to determine if the script can continue or something failed
            _progress = createClient(false);
            if (!_progress) return;
            //Checks if table exists otherwise makes it
            CreateTable(dbTableName, itemAttributes, keySchema, tableProvisionedThroughput).Wait();
            //Loads the table into a variable for later use
            try
            {
                statsTable = Table.LoadTable(client, dbTableName);
            }
            catch
            {
                Console.WriteLine(" -- Unable to access the table --");
            }

            //Add stats to the table
            var newItemDocument = new Document();
            newItemDocument["SteamID64"] = ID;
            newItemDocument[serverName] = Document.FromJson(statsJson);
            AddNewStats(newItemDocument).Wait();
            //Display the stats record
#if DEBUG
            ReadStatsTable(ID).Wait();
#endif

            //Rest of the program updates stats, don't update if not needed
            if (!_progress)
                return;
            UpdateStats(ID, serverName, statsDictionary).Wait();
            //Display the stats record
#if DEBUG
            ReadStatsTable(ID).Wait();
#endif
        }
    }
}