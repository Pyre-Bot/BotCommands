﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

namespace BotCommands_Dynamo
{
    public static partial class Dynamo
    {
        //Adds new stats if they don't exist
        private static async Task AddNewItem(Document newItem)
        {
            string ID = newItem["SteamID64"];
            if (!await ReadItem(ID)) //Only runs if ReadItem returns false
                try
                {
                    var writeNew = itemTable.PutItemAsync(newItem, token);
                    await writeNew;
                    _progress = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" -- Failed to write new data: " + ex.Message);
                }
            else
                Console.WriteLine(" -- Table already exists --");
        }

        //Attempts to read the stats, used to know if we need to add new ones
        private static async Task<bool> ReadItem(string ID)
        {
            var hash = new Primitive(ID, true);
            try
            {
                var readItem = itemTable.GetItemAsync(hash, token);
                item_record = await readItem;

                if (item_record == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(" -- Failed to get stats: " + ex.Message);
            }

            return false;
        }

        //Gets the actual contents of the table for output or other user
        public static async Task<bool> ReadStatsTable(long ID)
        {
            var hash = new Primitive(ID.ToString(), true);

            try
            {
                var readStats = itemTable.GetItemAsync(hash, token);
                item_record = await readStats;
                if (item_record == null) return false;

                Console.WriteLine(" -- Found record:\n" + item_record.ToJsonPretty());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(" -- Failed to get stats: " + ex.Message);
            }

            return false;
        }

        //Only ran if the information is already in table, updates to new values
        private static async Task UpdateStats(long ID, string serverName, Dictionary<string, string> statsDictionary)
        {
            try
            {
                try
                {
                    //Gets the results and turns them into JSON
                    var json = item_record.ToJsonPretty();
                    //Turns the JSON into a dictionary
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    //Removes what we don't want
                    foreach (var kvp in dict.Where(kvp => kvp.Key != serverName)) dict.Remove(kvp.Key);
                    var dictionaryString = dict.Aggregate("",
                        (current, keyValues) => current + keyValues.Key + " : " + keyValues.Value + ", ");
                    dictionaryString = string.Join("",
                        dictionaryString.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                    dictionaryString = dictionaryString.Substring(dictionaryString.LastIndexOf("{"));
                    dictionaryString = dictionaryString.Substring(0, dictionaryString.LastIndexOf("}") + 1);
                    Console.WriteLine(dictionaryString);
                    //Once again turns the string into ANOTHER dictionary
                    var jsonDict =
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(dictionaryString);
                    foreach (var kvp in jsonDict)
                        if (statsDictionary.ContainsKey(kvp.Key))
                        {
                            if (!kvp.Key.Contains("highest") && !kvp.Key.Contains("max") &&
                                !kvp.Key.Contains("longest"))
                            {
                                var number1 = double.Parse(statsDictionary[kvp.Key]);
                                var number2 = double.Parse(kvp.Value);
                                statsDictionary[kvp.Key] = (number1 + number2).ToString();
                            }
                            else if (kvp.Key.Contains("highest") || kvp.Key.Contains("max") ||
                                     kvp.Key.Contains("longest"))
                            {
                                if (double.Parse(kvp.Value) > double.Parse(statsDictionary[kvp.Key]))
                                    statsDictionary[kvp.Key] = kvp.Value;
                            }
                        }
                        else
                        {
                            statsDictionary[kvp.Key] = kvp.Value;
                        }

                    var statsJson2 = JsonConvert.SerializeObject(statsDictionary);
                    var updateDocument = new Document();
                    updateDocument["SteamID64"] = ID;
                    updateDocument[serverName] = Document.FromJson(statsJson2);
                    var writeNew = itemTable.UpdateItemAsync(updateDocument, token);
                    await writeNew;
                }
                catch (Exception ex)
                {
                    try
                    {
                        var statsJson2 = JsonConvert.SerializeObject(statsDictionary);
                        var updateDocument = new Document();
                        updateDocument["SteamID64"] = ID;
                        updateDocument[serverName] = Document.FromJson(statsJson2);
                        var writeNew = itemTable.UpdateItemAsync(updateDocument, token);
                        await writeNew;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}