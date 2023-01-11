using System;
using System.Net.Sockets;
using Amazon.DynamoDBv2;

namespace BotCommands_Dynamo
{
    public static partial class Dynamo
    {
        //Creates the connection to the database
        private static bool CreateClient(bool useDynamoDbLocal)
        {
            if (useDynamoDbLocal)
            {
                var localFound = false;
                try
                {
                    using var tcpClient = new TcpClient();
                    var result = tcpClient.BeginConnect("localhost", 8000, null, null);
                    localFound = result.AsyncWaitHandle.WaitOne(3000);
                    tcpClient.EndConnect(result);
                }
                catch
                {
                    localFound = false;
                }

                //If unable to find a local client
                if (!localFound)
                {
                    Console.WriteLine(" -- ERROR: Unable to connect to a local DynamoDB instance --");
                    return false;
                }

                //Otherwise proceeds:
                Console.WriteLine(" -- Setting up a connection to local database --");
                var ddbConfig = new AmazonDynamoDBConfig();
                ddbConfig.ServiceURL = "http://localhost:8000";
                try
                {
                    client = new AmazonDynamoDBClient(ddbConfig);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" -- Failed to create client: " + ex.Message);
                    return false;
                }
            }
            else
            {
                //Connect to DynamoDB web server if set to false
                try
                {
                    client = new AmazonDynamoDBClient();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" -- Failed to create client: " + ex.Message);
                    return false;
                }
            }

            return true;
        }
    }
}