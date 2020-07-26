using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
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
        private static TableDescription description;
        private static Table itemTable;
        private static readonly CancellationTokenSource source = new CancellationTokenSource();
        private static readonly CancellationToken token = source.Token;
        private static Document item_record;

        //Used to tell if we errored and need to exit
        private static bool _progress;

        public static void Main(string[] args)
        {
            // Used for testing, overwrote in production
            long ID = 123456789;
            // Declares variable to be used later
            var serverName = "";


            // Sets desired throughput, in this case 5 in, 5 out
            var tableProvisionedThroughput = new ProvisionedThroughput(5, 5);

            // Stat objects
            // Primary key info
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
            // Variables used for dictionary
            var arguments = string.Join(" ", args);
            var statsDictionary = arguments.Split(' ')
                .Select(p => p.Trim().Split(','))
                .ToDictionary(p => p[0], p => p[1]);
            foreach (var kvp in statsDictionary)
            {
                if (kvp.Key == "Server")
                {
                    serverName = kvp.Value;
                    statsDictionary.Remove(kvp.Key);
                }

                if (kvp.Key == "SteamID")
                {
                    ID = long.Parse(kvp.Value);
                    statsDictionary.Remove(kvp.Key);
                }
            }

            // Variables used for DynamoDB table
            const string dbTableName = "BotCommands_Stats";

            // Turns dictionary into JSON
            var statsJson = JsonConvert.SerializeObject(statsDictionary);

            // Lets do things!
            Console.WriteLine(" -- Attempting to connect to the database --");
            // Create connection to the database
            // Progress variable is used to determine if the script can continue or something failed
            _progress = CreateClient(false);
            if (!_progress) return;
            // Checks if table exists otherwise makes it
            // TODO: Consider removing this?
            CreateTable(dbTableName, itemAttributes, keySchema, tableProvisionedThroughput).Wait();
            // Loads the table into a variable for later use
            try
            {
                itemTable = Table.LoadTable(client, dbTableName);
            }
            catch
            {
                Console.WriteLine(" -- Unable to access the table --");
            }

            // Add stats to the table
            var newItemDocument = new Document {["SteamID64"] = ID, [serverName] = Document.FromJson(statsJson)};
            AddNewItem(newItemDocument).Wait();

            // Rest of the program updates stats, don't update if not needed
            if (!_progress)
                return;
            UpdateStats(ID, serverName, statsDictionary).Wait();
        }
    }
}