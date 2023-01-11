using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;

namespace BotCommands_Dynamo
{
    public static partial class Dynamo
    {
        //Checks table existence or calls the function to create a new table
        private static async Task CreateTable(string newTableName,
            List<AttributeDefinition> tableAttributes,
            List<KeySchemaElement> tableKeySchema,
            ProvisionedThroughput provisionedThroughput)
        {
            if (!await CheckTableExistence(newTableName))
            {
                Console.WriteLine(" -- Creating new table --");
                var newTable =
                    CreateNewTable(newTableName, tableAttributes, tableKeySchema, provisionedThroughput);
                await newTable;
            }
        }

        //Function for checking if the table already exists
        private static async Task<bool> CheckTableExistence(string tableName)
        {
            var tableResponse = await client.ListTablesAsync();

            if (!tableResponse.TableNames.Contains(tableName)) return false;
            DescribeTableResponse describeTable;
            try
            {
                describeTable = await client.DescribeTableAsync(tableName);
            }
            catch
            {
                description = null;
                return false;
            }

            description = describeTable.Table;
            return true;
        }

        //Function to create a new table if it doesn't exist already
        private static async Task<bool> CreateNewTable(string tableName,
            List<AttributeDefinition> tableAttributes,
            List<KeySchemaElement> tableKeySchema,
            ProvisionedThroughput provisionedThroughput)
        {
            CreateTableResponse response;

            var request = new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions = tableAttributes,
                KeySchema = tableKeySchema,
                ProvisionedThroughput = provisionedThroughput
            };

            try
            {
                var makeTable = client.CreateTableAsync(request);
                response = await makeTable;
            }
            catch
            {
                return false;
            }

            Console.WriteLine(" -- Status of new table: {0}", response.TableDescription.TableStatus);
            description = response.TableDescription;
            return true;
        }
    }
}