using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

namespace BotCommands_Dynamo
{
    public static partial class Dynamo
    {
        //Adds new stats if they don't exist
        private static async Task AddNewStats(Document newItem)
        {
            string ID = newItem["SteamID64"];
            if (!await ReadStats(ID)) //Only runs if ReadStats returns false (no info in table)
                try
                {
                    var writeNew = statsTable.PutItemAsync(newItem, token);
                    await writeNew;
                    _progress = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" -- Failed to write new data: " + ex.Message);
                }
            else
                Console.WriteLine(" -- Stats already exists --");
        }

        //Attempts to read the stats, used to know if we need to add new ones
        private static async Task<bool> ReadStats(string ID)
        {
            var hash = new Primitive(ID, true);
            try
            {
                var readStats = statsTable.GetItemAsync(hash, token);
                stats_record = await readStats;

                if (stats_record == null)
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
                var readStats = statsTable.GetItemAsync(hash, token);
                stats_record = await readStats;
                if (stats_record == null)
                {
                    return false;
                }

                Console.WriteLine(" -- Found record:\n" + stats_record.ToJsonPretty());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(" -- Failed to get stats: " + ex.Message);
            }

            return false;
        }

        //Only ran if the information is already in table, updates to new values
        //This fucking sucked
        private static async Task UpdateStats(long ID, string serverName, Dictionary<string, string> statsDictionary)
        {
            try
            {
                try
                {
                    //Gets the results and turns them into JSON
                    var json = stats_record.ToJsonPretty();
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
                    //Once again turns the string into ANOTHER dictionary
                    var jsonDict =
                        JsonConvert.DeserializeObject<Dictionary<string, int>>(dictionaryString);
                    foreach (KeyValuePair<string, int> kvp in jsonDict)
                    {
                        if (statsDictionary.ContainsKey(kvp.Key))
                        {
                            var number1 = double.Parse(statsDictionary[kvp.Key]);
                            var number2 = double.Parse(kvp.Value.ToString());
                            statsDictionary[kvp.Key] = (number1 + number2).ToString();
                        }
                        else
                        {
                            statsDictionary[kvp.Key] = kvp.Value.ToString();
                        }
                    }
                    var statsJson2 = JsonConvert.SerializeObject(statsDictionary);
                    var updateDocument = new Document();
                    updateDocument["SteamID64"] = ID;
                    updateDocument[serverName] = Document.FromJson(statsJson2);
                    var writeNew = statsTable.UpdateItemAsync(updateDocument, token);
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
                        var writeNew = statsTable.UpdateItemAsync(updateDocument, token);
                        await writeNew;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception.Message);
                    }
                }
                
                //Takes the dictionary and turns it into a string again so we can edit values easier
                // try
                // {
                //     try
                //     {
                //         dictionaryString = string.Join("",
                //             dictionaryString.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                //         dictionaryString = dictionaryString.Substring(dictionaryString.LastIndexOf("{"));
                //         dictionaryString = dictionaryString.Substring(0, dictionaryString.LastIndexOf("}") + 1);
                //         //Once again turns the string into ANOTHER dictionary
                //         var jsonDict =
                //             JsonConvert.DeserializeObject<Dictionary<string, int>>(dictionaryString);
                //         //Updates the values to include the information from current run
                //         jsonDict["totalTimeAlive"] += statsDictionary["totalTimeAlive"];
                //         jsonDict["totalKills"] += statsDictionary["totalKills"];
                //         jsonDict["totalDeaths"] += statsDictionary["totalDeaths"];
                //         jsonDict["totalGoldCollected"] += statsDictionary["totalGoldCollected"];
                //         jsonDict["totalItemsCollected"] += statsDictionary["totalItemsCollected"];
                //         jsonDict["totalStagesCompleted"] += statsDictionary["totalStagesCompleted"];
                //         jsonDict["totalPurchases"] += statsDictionary["totalPurchases"];
                //         //Turns the dictionary into a string ONE MORE TIME.....
                //         var statsJson2 = JsonConvert.SerializeObject(jsonDict);
                //         //Creates a new document so we can update the value
                //         var updateDocument = new Document();
                //         updateDocument["SteamID64"] = ID;
                //         updateDocument[serverName] = Document.FromJson(statsJson2);
                //         var writeNew = statsTable.UpdateItemAsync(updateDocument, token);
                //         await writeNew;
                //     }
                //     catch (Exception ex)
                //     {
                //         Console.WriteLine(ex.Message);
                //     }
                // }
                // catch
                // {
                //     try
                //     {
                //         var jsonDict = new Dictionary<string, int>
                //         {
                //             {"totalTimeAlive", 0},
                //             {"totalKills", 0},
                //             {"totalDeaths", 0},
                //             {"totalGoldCollected", 0},
                //             {"totalItemsCollected", 0},
                //             {"totalStagesCompleted", 0},
                //             {"totalPurchases", 0}
                //         };
                //         //Updates the values to include the information from current run
                //         jsonDict["totalTimeAlive"] += statsDictionary["totalTimeAlive"];
                //         jsonDict["totalKills"] += statsDictionary["totalKills"];
                //         jsonDict["totalDeaths"] += statsDictionary["totalDeaths"];
                //         jsonDict["totalGoldCollected"] += statsDictionary["totalGoldCollected"];
                //         jsonDict["totalItemsCollected"] += statsDictionary["totalItemsCollected"];
                //         jsonDict["totalStagesCompleted"] += statsDictionary["totalStagesCompleted"];
                //         jsonDict["totalPurchases"] += statsDictionary["totalPurchases"];
                //         //Turns the dictionary into a string ONE MORE TIME.....
                //         var statsJson2 = JsonConvert.SerializeObject(jsonDict);
                //         //Creates a new document so we can update the value
                //         var updateDocument = new Document();
                //         updateDocument["SteamID64"] = ID;
                //         updateDocument[serverName] = Document.FromJson(statsJson2);
                //         var writeNew = statsTable.UpdateItemAsync(updateDocument, token);
                //         await writeNew;
                //     }
                //     catch (Exception ex)
                //     {
                //         Console.WriteLine(ex.Message);
                //     }
                // }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}