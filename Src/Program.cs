using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection.PortableExecutable;
using System.ComponentModel.Design;
using System.IO.IsolatedStorage;

using Arshu.FlyDeploy.Utility;
using Arshu.FlyDeploy.Model;

namespace Arshu.AppBak
{
    public class Program
    {
        public const string FlyCommand = "flyctl";
        public const string FlyApiHostUrl = "http://_api.internal:4280";
        public const string ConfigDirName = "Config";
        public const string ProcessConfigDirName = "Process";
        public const string MachineConfigDirName = "Machine";
        public const string ProcessJsonFileName = "process.json";

        #region HttpClient

        private static HttpClient? httpClient = null;
        private static HttpClient GetClient(string apiHostUrl)
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(apiHostUrl);
            }

            return httpClient;
        }

        #endregion

        #region Json Utility

        public static T? DeserializeObject<T>(string jsonString)
        {
            T? retObj = default(T);

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                retObj = JsonSerializer.Deserialize<T>(jsonString, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                System.Diagnostics.Debugger.Break();
            }

            return retObj;
        }

        public static string SerializeObject(object jsonObject)
        {
            string jsonString = String.Empty;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
            };
            string retJsonString = JsonSerializer.Serialize(jsonObject, options);
            if (retJsonString != null) { jsonString = retJsonString; }

            return jsonString;
        }

        #endregion

        #region Command Utility

        private static JsonArray GetFlyJsonArray(string flyCommand, string flyQuery, string fly_api_token)
        {
            JsonArray flyJsonArray = new JsonArray();

            try
            {
                List<string> processOutput = new List<string>();
                Dictionary<string, string> envVariableList = new Dictionary<string, string>();
                envVariableList.Add("FLY_ACCESS_TOKEN", fly_api_token);
                processOutput = new ProcessExecute(flyCommand, flyQuery + " --access-token " + fly_api_token + " --json", envVariableList).Run();
                string jsonOuput = string.Join("", processOutput.ToArray());
                JsonArray? retFlyJsonArray = DeserializeObject<JsonArray>(jsonOuput);
                if (retFlyJsonArray != null) flyJsonArray = retFlyJsonArray;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return flyJsonArray;
        }

        private static JsonObject GetFlyJson(string flyCommand, string flyQuery, string fly_api_token)
        {
            JsonObject orgJson = new JsonObject();

            try
            {

                List<string> processOutput = new List<string>();
                Dictionary<string, string> envVariableList = new Dictionary<string, string>();
                envVariableList.Add("FLY_ACCESS_TOKEN", fly_api_token);
                processOutput = new ProcessExecute(flyCommand, flyQuery + " --access-token " + fly_api_token + " --json", envVariableList).Run();
                string jsonOuput = string.Join("", processOutput.ToArray());
                JsonObject? retOrgJson = DeserializeObject<JsonObject>(jsonOuput);
                if (retOrgJson != null) orgJson = retOrgJson;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return orgJson;
        }

        private static List<string> GetFlyCommand(string flyCommand, string flyQuery, string fly_api_token)
        {
            List<string> processOutput = new List<string>();
            try
            {
                Dictionary<string, string> envVariableList = new Dictionary<string, string>();
                envVariableList.Add("FLY_ACCESS_TOKEN", fly_api_token);
                processOutput = new ProcessExecute(flyCommand, flyQuery + " --access-token " + fly_api_token, envVariableList).Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return processOutput;
        }

        #endregion

        public static async Task Main(string[] args)
        {
            #region Get CurrentDirectory and Default Config Process List

            string currentDirectory = Directory.GetCurrentDirectory();
            string currentConfigDirectory = Path.Combine(currentDirectory, ConfigDirName);
            string currentProcessConfigDirectory = Path.Combine(currentConfigDirectory, ProcessConfigDirName);
            string currentMachineConfigDirectory = Path.Combine(currentConfigDirectory, MachineConfigDirName);

            string processConfigFilePath = Path.Combine(currentProcessConfigDirectory, ProcessJsonFileName);
            string processConfigText = "";
            if (File.Exists(processConfigFilePath) == true)
            {
                processConfigText = await File.ReadAllTextAsync(processConfigFilePath);
            }
            else
            {
                DirectoryInfo currentDirectoryInfo = new DirectoryInfo(currentDirectory);
                if (currentDirectoryInfo.Parent != null)
                {
                    currentConfigDirectory = Path.Combine(currentDirectoryInfo.Parent.FullName, ProcessJsonFileName);
                    currentProcessConfigDirectory = Path.Combine(currentConfigDirectory, ProcessConfigDirName);
                    currentMachineConfigDirectory = Path.Combine(currentConfigDirectory, MachineConfigDirName);

                    processConfigFilePath = Path.Combine(currentProcessConfigDirectory, ProcessJsonFileName);
                    if (File.Exists(processConfigFilePath) == true)
                    {
                        processConfigText = await File.ReadAllTextAsync(processConfigFilePath);
                        currentDirectory = currentDirectoryInfo.Parent.FullName;
                    }
                }
            }

            ProcessConfig? processConfig = null;
            if (string.IsNullOrEmpty(processConfigText) == false)
            {
                processConfig = DeserializeObject<ProcessConfig>(processConfigText);
            }

            #endregion

            if ((processConfig != null) && (processConfig.ProcessList.Count > 0))
            {
                foreach (ProcessConfigInfo itemConfigInfo in processConfig.ProcessList)
                {
                    if ((itemConfigInfo != null) && (itemConfigInfo.Process == true))
                    {
                        long deployStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                        ActionConfig? actionConfig = null;

                        #region Get App Process Config

                        string processAppConfigFilePath = "";
                        if (itemConfigInfo.ConfigPath.Contains(":") == true)
                        {
                            processAppConfigFilePath = itemConfigInfo.ConfigPath;
                        }
                        else
                        {
                            processAppConfigFilePath = Path.Combine(currentProcessConfigDirectory, itemConfigInfo.ConfigPath);
                        }
                        string processAppConfigText = "";
                        if (File.Exists(processAppConfigFilePath) == true)
                        {
                            processAppConfigText = await File.ReadAllTextAsync(processAppConfigFilePath);
                        }

                        if (string.IsNullOrEmpty(processAppConfigText) == false)
                        {
                            actionConfig = DeserializeObject<ActionConfig>(processAppConfigText);
                        }

                        #endregion

                        if (actionConfig != null)
                        {
                            Console.WriteLine("Processing Deploy Config [" + itemConfigInfo.ConfigPath + "] with Action Interval [" + actionConfig.ActionInterval + "ms]");

                            #region Process Deploy Config

                            await ProcessConfig(deployStartTimestamp, currentMachineConfigDirectory, actionConfig);

                            #endregion

                            long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;
                            Console.WriteLine("Completed Processing of Action Config [" + itemConfigInfo.ConfigPath + "] in [" + deployDiffTime.ToString("####.##") + "sec]");
                        }
                        else
                        {
                            Console.WriteLine("Not Found Action Config [" + itemConfigInfo.ConfigPath + "]");
                        }

                        //Console.WriteLine("Press Enter key to execute the next deploy config");
                        //Console.ReadLine();
                    }
                }
            }
            else
            {
                Console.WriteLine("Not Found Process Config under Directory [" + currentConfigDirectory + "]");
            }

            Console.WriteLine("Press Enter key to exit");
            Console.ReadLine();
        }

        #region Fly Api Utilities

        private static async Task<JsonArray> GetFlyMachineList(string fly_api_token, string app_name, bool showInvalidMachines)
        {
            JsonArray jsonMachineList = new JsonArray();

            try
            {
                JsonArray? allJsonMachineList = new JsonArray();

                #region Get All Machine List

                //string flyQuery = "machine list --app " + actionConfig.AppName;
                //JsonArray allJsonMachineList = GetFlyJsonArray(FlyCommand, flyQuery, actionConfig.ApiToken);

                HttpClient httpClient = GetClient(FlyApiHostUrl);
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fly_api_token);

                    var getResponse = httpClient.GetAsync("/v1/apps/" + app_name + "/machines").Result;
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var jsonContent = await getResponse.Content.ReadAsStringAsync();
                        allJsonMachineList = DeserializeObject<JsonArray>(jsonContent);
                    }
                }

                #endregion

                #region Filter All Machine List

                if (allJsonMachineList != null)
                {
                    foreach (object? existingItem in allJsonMachineList)
                    {
                        if (existingItem != null)
                        {
                            JsonObject existingItemMachine = (JsonObject)existingItem;
                            if (existingItemMachine != null)
                            {
                                string? existingMachineName = "";
                                if (existingItemMachine.ContainsKey("name") == true)
                                {
                                    object? existingItemMachineName = existingItemMachine["name"];
                                    if (existingItemMachineName != null)
                                    {
                                        existingMachineName = existingItemMachineName.ToString();
                                    }
                                }
                                string? existingMachineState = "";
                                if (existingItemMachine.ContainsKey("state") == true)
                                {
                                    object? existingItemMachineState = existingItemMachine["state"];
                                    if (existingItemMachineState != null)
                                    {
                                        existingMachineState = existingItemMachineState.ToString();
                                    }
                                }
                                if (string.IsNullOrEmpty(existingMachineState) == false)
                                {
                                    //Skip destroyed, replacing Machines
                                    if (((existingMachineState.Contains("destroy", StringComparison.OrdinalIgnoreCase) == false)
                                        && (existingMachineState.Contains("replac", StringComparison.OrdinalIgnoreCase) == false)
                                        )
                                        || (showInvalidMachines == true))
                                    {
                                        JsonNode? existingMachineNode = JsonNode.Parse(existingItemMachine.ToJsonString());
                                        if (existingMachineNode != null)
                                        {
                                            JsonObject? existingMachineJson = existingMachineNode as JsonObject;
                                            if (existingMachineJson != null)
                                            {
                                                jsonMachineList.Add(existingMachineJson);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetFlyMachineList" + ex.Message);
            }

            return jsonMachineList;
        }

        private static async Task<bool> CreateMachine(string apiToken, string appName, MachineConfig machineConfig, JsonObject createConfigJson, string machineName, string machineRegion, long deployStartTimestamp)
        {
            bool ret = false;
            long methodStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            #region Append Env Variables to Config

            if (machineConfig.EnvConfig.Count > 0)
            {
                if (createConfigJson.ContainsKey("config") == true)
                {
                    JsonObject? configObject = createConfigJson["config"] as JsonObject;
                    if (configObject != null)
                    {
                        if (configObject.ContainsKey("env") == true)
                        {
                            JsonObject? envObject = configObject["env"] as JsonObject;
                            if (envObject != null)
                            {
                                foreach (var itemEnv in machineConfig.EnvConfig)
                                {
                                    if (envObject.ContainsKey(itemEnv.EnvName) == false)
                                    {
                                        if (itemEnv.EnvValue != null)
                                        {
                                            envObject.Add(itemEnv.EnvName, itemEnv.EnvValue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Create Machine Request

            string requestString = SerializeObject(createConfigJson);
            requestString = requestString.Replace("{{$MachineName}}", machineName);
            requestString = requestString.Replace("{{$Region}}", machineRegion);
            requestString = requestString.Replace("{{$AppImage}}", machineConfig.DockerImage);

            #endregion

            HttpClient httpClient = GetClient(FlyApiHostUrl);
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                #region Create Machine

                StringContent postData = new StringContent(requestString);
                postData.Headers.Remove("Content-Type");
                postData.Headers.Add("Content-Type", "application/json");

                var response = httpClient.PostAsync("/v1/apps/" + appName + "/machines", postData).Result;

                #region Process Response

                var jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                JsonObject? retObj = JsonSerializer.Deserialize<JsonObject>(jsonContent, options);
                string errorMessage = "";
                if ((retObj != null) && (retObj.ContainsKey("error")))
                {
                    var errorObj = retObj["error"];
                    if (errorObj != null)
                    {
                        errorMessage = errorObj.ToString();
                    }
                }

                #endregion

                if (response.IsSuccessStatusCode)
                {
                    long methodDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - methodStartTimestamp;
                    long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;

                    string? privateIp = "";
                    if (retObj != null)
                    {
                        if (retObj.ContainsKey("private_ip") == true)
                        {
                            privateIp = ((string?)retObj["private_ip"]);
                            ret = true;
                        }
                    }
                    Console.WriteLine("Created Machine [" + machineName + "] having Private IP [" + privateIp + "] Successfully for AppName [" + appName + "] under Region [" + machineRegion + "] using Docker Image [" + machineConfig.DockerImage + "][" + deployDiffTime.ToString("####.##") + "sec][" + methodDiffTime.ToString("####.##") + "sec]");
                }
                else
                {
                    Console.WriteLine("Error [" + errorMessage + "] in Creating Machine [" + machineName + "] for AppName [" + appName + "] under Region [" + machineRegion + "] using Docker Image [" + machineConfig.DockerImage + "]");
                }

                #endregion
            }

            return ret;
        }

        private static async Task<bool> UpdateMachine(string apiToken, string appName, MachineConfig machineConfig, JsonObject updateConfigJson, string machineID, string machineName, string machineRegion, long deployStartTimestamp)
        {
            bool ret = false;
            long methodStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            #region Append Env Variables to Config

            if (machineConfig.EnvConfig.Count > 0)
            {
                if (updateConfigJson.ContainsKey("config") == true)
                {
                    JsonObject? configObject = updateConfigJson["config"] as JsonObject;
                    if (configObject != null)
                    {
                        if (configObject.ContainsKey("env") == true)
                        {
                            JsonObject? envObject = configObject["env"] as JsonObject;
                            if (envObject != null)
                            {
                                foreach (var itemEnv in machineConfig.EnvConfig)
                                {
                                    if (envObject.ContainsKey(itemEnv.EnvName) == false)
                                    {
                                        if (itemEnv.EnvValue != null)
                                        {
                                            envObject.Add(itemEnv.EnvName, itemEnv.EnvValue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Update Machine Request

            string requestString = SerializeObject(updateConfigJson);
            requestString = requestString.Replace("{{$AppImage}}", machineConfig.DockerImage);

            #endregion

            HttpClient httpClient = GetClient(FlyApiHostUrl);
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                #region Update Machine

                StringContent postData = new StringContent(requestString);
                postData.Headers.Remove("Content-Type");
                postData.Headers.Add("Content-Type", "application/json");

                var response = httpClient.PostAsync("/v1/apps/" + appName + "/machines/" + machineID, postData).Result;

                #region Process Response

                var jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                JsonObject? retObj = JsonSerializer.Deserialize<JsonObject>(jsonContent, options);
                string errorMessage = "";
                if ((retObj != null) && (retObj.ContainsKey("error")))
                {
                    var errorObj = retObj["error"];
                    if (errorObj != null)
                    {
                        errorMessage = errorObj.ToString();
                    }
                }

                #endregion

                if (response.IsSuccessStatusCode)
                {
                    long methodDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - methodStartTimestamp;
                    long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;

                    string? privateIp = "";
                    if (retObj != null)
                    {
                        if (retObj.ContainsKey("private_ip") == true)
                        {
                            privateIp = ((string?)retObj["private_ip"]);
                            ret = true;
                        }
                    }
                    Console.WriteLine("Updated Machine [" + machineName + "][" + machineID + "] having Private IP [" + privateIp + "] Successfully for AppName [" + appName + "] under Region [" + machineRegion + "] using Docker Image [" + machineConfig.DockerImage + "][" + deployDiffTime.ToString("####.##") + "sec][" + methodDiffTime.ToString("####.##") + "sec]");
                }
                else
                {
                    Console.WriteLine("Error [" + errorMessage + "] in Updating Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "] using Docker Image [" + machineConfig.DockerImage + "]");
                }

                #endregion
            }

            return ret;
        }

        private static async Task<bool> StopMachine(string apiToken, string appName, string machineID, string machineName, string machineRegion, long deployStartTimestamp)
        {
            bool ret = false;
            long methodStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            HttpClient httpClient = GetClient(FlyApiHostUrl);
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                #region Stop Machine

                StringContent postData = new StringContent("");
                postData.Headers.Remove("Content-Type");
                postData.Headers.Add("Content-Type", "application/json");

                var response = httpClient.PostAsync("/v1/apps/" + appName + "/machines/" + machineID + "/stop", postData).Result;

                #region Process Response

                var jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                JsonObject? retObj = JsonSerializer.Deserialize<JsonObject>(jsonContent, options);
                string errorMessage = "";
                if ((retObj != null) && (retObj.ContainsKey("error")))
                {
                    var errorObj = retObj["error"];
                    if (errorObj != null)
                    {
                        errorMessage = errorObj.ToString();
                    }
                }

                #endregion

                if (response.IsSuccessStatusCode)
                {
                    long methodDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - methodStartTimestamp;
                    long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;

                    if (retObj != null)
                    {
                        if (retObj.ContainsKey("ok") == true)
                        {
                            object? okObj = retObj["ok"];
                            if (okObj != null)
                            {
                                string? okVal = okObj.ToString();
                                if (string.IsNullOrEmpty(okVal) == false)
                                {
                                    if (bool.TryParse(okVal, out bool isStopped) == true)
                                    {
                                        ret = isStopped;
                                    }
                                }
                            }
                        }
                    }
                    if (ret == true)
                    {
                        Console.WriteLine("Stopped Machine [" + machineName + "][" + machineID + "] Successfully for AppName [" + appName + "] under Region [" + machineRegion + "][" + deployDiffTime.ToString("####.##") + "sec][" + methodDiffTime.ToString("####.##") + "sec]");
                    }
                    else
                    {
                        Console.WriteLine("Error [" + errorMessage + "] in Stoping Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                    }
                }
                else
                {
                    Console.WriteLine("Error [" + errorMessage + "] in Stoping Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                }

                #endregion
            }

            return ret;
        }

        private static async Task<bool> StartMachine(string apiToken, string appName, string machineID, string machineName, string machineRegion, long deployStartTimestamp)
        {
            bool ret = false;
            long methodStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            HttpClient httpClient = GetClient(FlyApiHostUrl);
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                #region Start Machine

                StringContent postData = new StringContent("");
                postData.Headers.Remove("Content-Type");
                postData.Headers.Add("Content-Type", "application/json");

                var response = httpClient.PostAsync("/v1/apps/" + appName + "/machines/" + machineID + "/start", postData).Result;

                #region Process Response

                var jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                JsonObject? retObj = JsonSerializer.Deserialize<JsonObject>(jsonContent, options);
                string errorMessage = "";
                if ((retObj != null) && (retObj.ContainsKey("error")))
                {
                    var errorObj = retObj["error"];
                    if (errorObj != null)
                    {
                        errorMessage = errorObj.ToString();
                    }
                }

                #endregion

                if (response.IsSuccessStatusCode)
                {
                    long methodDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - methodStartTimestamp;
                    long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;

                    if (retObj != null)
                    {
                        if (retObj.ContainsKey("ok") == true)
                        {
                            object? okObj = retObj["ok"];
                            if (okObj != null)
                            {
                                string? okVal = okObj.ToString();
                                if (string.IsNullOrEmpty(okVal) == false)
                                {
                                    if (bool.TryParse(okVal, out bool isStopped) == true)
                                    {
                                        ret = isStopped;
                                    }
                                }
                            }
                        }
                    }
                    if (ret == true)
                    {
                        Console.WriteLine("Stopped Machine [" + machineName + "][" + machineID + "] Successfully for AppName [" + appName + "] under Region [" + machineRegion + "][" + deployDiffTime.ToString("####.##") + "sec][" + methodDiffTime.ToString("####.##") + "sec]");
                    }
                    else
                    {
                        Console.WriteLine("Error [" + errorMessage + "] in Stoping Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                    }
                }
                else
                {
                    Console.WriteLine("Error [" + errorMessage + "] in Stoping Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                }

                #endregion
            }

            return ret;
        }

        private static async Task<bool> DeleteMachine(string apiToken, string appName, string machineID, string machineName, string machineRegion, long deployStartTimestamp)
        {
            bool ret = false;
            long methodStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            HttpClient httpClient = GetClient(FlyApiHostUrl);
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                #region Delete Machine

                StringContent postData = new StringContent("");
                postData.Headers.Remove("Content-Type");
                postData.Headers.Add("Content-Type", "application/json");

                var response = httpClient.DeleteAsync("/v1/apps/" + appName + "/machines/" + machineID).Result;

                #region Process Response

                var jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };
                JsonObject? retObj = JsonSerializer.Deserialize<JsonObject>(jsonContent, options);
                string errorMessage = "";
                if ((retObj != null) && (retObj.ContainsKey("error")))
                {
                    var errorObj = retObj["error"];
                    if (errorObj != null)
                    {
                        errorMessage = errorObj.ToString();
                    }
                }

                #endregion

                if (response.IsSuccessStatusCode)
                {
                    long methodDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - methodStartTimestamp;
                    long deployDiffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - deployStartTimestamp;

                    if (retObj != null)
                    {
                        if (retObj.ContainsKey("ok") == true)
                        {
                            object? okObj = retObj["ok"];
                            if (okObj != null)
                            {
                                string? okVal = okObj.ToString();
                                if (string.IsNullOrEmpty(okVal) == false)
                                {
                                    if (bool.TryParse(okVal, out bool isDeleted) == true)
                                    {
                                        ret = isDeleted;
                                    }
                                }
                            }
                        }
                    }
                    if (ret == true)
                    {
                        Console.WriteLine("Deleted Machine [" + machineName + "][" + machineID + "] Successfully for AppName [" + appName + "] under Region [" + machineRegion + "][" + deployDiffTime.ToString("####.##") + "sec][" + methodDiffTime.ToString("####.##") + "sec]");
                    }
                    else
                    {
                        Console.WriteLine("Error [" + errorMessage + "] in Deleting Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                    }
                }
                else
                {
                    Console.WriteLine("Error [" + errorMessage + "] in Deleting Machine [" + machineName + "][" + machineID + "] for AppName [" + appName + "] under Region [" + machineRegion + "]");
                }

                #endregion
            }

            return ret;
        }

        #endregion

        #region Action Utilities

        private static Dictionary<string, MachineInfo> GetActionMachineInfoList(JsonArray existingMachineList, string regionCode, string appName)
        {
            Dictionary<string, MachineInfo> actionMachineInfoList = new Dictionary<string, MachineInfo>();

            #region Create Change Machine Info List for the Region

            int machineCount = 1;
            bool haveMachine = false;
            string? machineRegion = regionCode;
            string key = machineRegion + "_" + machineCount;
            string? machineName = appName + "_" + regionCode + "_" + machineCount;
            string? machineID = "";
            foreach (object? item in existingMachineList)
            {
                if (item != null)
                {
                    JsonObject itemMachine = (JsonObject)item;
                    if (itemMachine != null)
                    {
                        key = machineRegion + "_" + machineCount;
                        machineName = appName + "_" + regionCode + "_" + machineCount;

                        if (itemMachine.ContainsKey("region") == true)
                        {
                            object? machineRegionObj = itemMachine["region"];
                            if (machineRegionObj != null)
                            {
                                machineRegion = machineRegionObj.ToString();
                            }
                        }
                        if (machineRegion == regionCode)
                        {
                            haveMachine = true;
                            if (itemMachine.ContainsKey("id") == true)
                            {
                                object? machineIdObj = itemMachine["id"];
                                if (machineIdObj != null)
                                {
                                    machineID = machineIdObj.ToString();
                                }
                            }
                            if (itemMachine.ContainsKey("name") == true)
                            {
                                object? machineNameObj = itemMachine["name"];
                                if (machineNameObj != null)
                                {
                                    machineName = machineNameObj.ToString();
                                }
                            }
                            if ((machineRegion != null) && (machineName != null))
                            {
                                MachineInfo machineInfo = new MachineInfo(machineRegion, machineName, machineID);
                                if (machineInfo.IsValid() == true)
                                {
                                    actionMachineInfoList.Add(key, machineInfo);
                                }
                            }

                            machineCount++;
                        }
                    }
                }
            }

            if ((haveMachine == false) && (machineRegion != null) && (machineName != null))
            {
                MachineInfo machineInfo = new MachineInfo(regionCode, machineName, machineID, true);
                if (machineInfo.IsValid() == true)
                {
                    actionMachineInfoList.Add(key, machineInfo);
                }
            }

            #endregion

            return actionMachineInfoList;
        }

        #endregion

        #region Process Config

        private static async Task<bool> ProcessConfig(long deployStartTimestamp, string currentMachineConfigDirectory, ActionConfig actionConfig)
        {
            bool ret = false;

            foreach (MachineConfig machineConfig in actionConfig.MachineConfig)
            {
                #region Create Config Json

                JsonObject createConfigJson = new JsonObject();
                string machineCreateConfigText = "";
                string machineCreateConfigFilePath = Path.Combine(currentMachineConfigDirectory, machineConfig.MachineCreateTemplate);
                if (File.Exists(machineCreateConfigFilePath) == true)
                {
                    machineCreateConfigText = await File.ReadAllTextAsync(machineCreateConfigFilePath);
                }
                if (string.IsNullOrEmpty(machineCreateConfigText) == false)
                {
                    JsonObject? createConfig = DeserializeObject<JsonObject>(machineCreateConfigText);
                    if (createConfig != null)
                    {
                        createConfigJson = createConfig;
                    }
                }

                #endregion

                #region Update Config Json

                JsonObject updateConfigJson = new JsonObject();
                string machineUpdateConfigText = "";
                string machineUpdateConfigFilePath = Path.Combine(currentMachineConfigDirectory, machineConfig.MachineUpdateTemplate);
                if (File.Exists(machineUpdateConfigFilePath) == true)
                {
                    machineUpdateConfigText = await File.ReadAllTextAsync(machineUpdateConfigFilePath);
                }
                if (string.IsNullOrEmpty(machineUpdateConfigText) == false)
                {
                    JsonObject? updateConfig = DeserializeObject<JsonObject>(machineUpdateConfigText);
                    if (updateConfig != null)
                    {
                        updateConfigJson = updateConfig;
                    }
                }

                #endregion

                if ((createConfigJson != null) && (createConfigJson.Count > 0) && (updateConfigJson != null) && (updateConfigJson.Count > 0))
                {
                    JsonArray existingMachineList = await GetFlyMachineList(actionConfig.ApiToken, actionConfig.AppName, false).ConfigureAwait(false);

                    if ((machineConfig.Action.Contains("CreateOrUpdate", StringComparison.OrdinalIgnoreCase) == true)
                        || (machineConfig.Action.Contains("Create", StringComparison.OrdinalIgnoreCase) == true)
                        || (machineConfig.Action.Contains("Update", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        foreach (RegionConfig itemRegion in actionConfig.RegionConfig)
                        {
                            if (itemRegion.Process == true)
                            {
                                Dictionary<string, MachineInfo> actionMachineInfoList = GetActionMachineInfoList(existingMachineList, itemRegion.RegionCode, actionConfig.AppName);

                                foreach (var item in actionMachineInfoList)
                                {
                                    MachineInfo machineInfo = item.Value;
                                    if ((machineInfo.Create == true) && (createConfigJson != null))
                                    {
                                        if ((machineConfig.Action.Contains("Create", StringComparison.OrdinalIgnoreCase) == true) && (string.IsNullOrEmpty(machineInfo.MachineID) == true))
                                        {
                                            await CreateMachine(actionConfig.ApiToken, actionConfig.AppName, machineConfig, createConfigJson, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Skipped Creating Machine since MachineID [" + machineInfo.MachineID + "]exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                        }
                                    }
                                    else if ((createConfigJson != null) && (updateConfigJson != null))
                                    {
                                        if ((machineConfig.Action.Contains("Update", StringComparison.OrdinalIgnoreCase) == true) && (string.IsNullOrEmpty(machineInfo.MachineID) == false))
                                        {
                                            if (machineConfig.ActionType == "Direct")
                                            {
                                                await UpdateMachine(actionConfig.ApiToken, actionConfig.AppName, machineConfig, updateConfigJson, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                            }
                                            else
                                            {
                                                int currentMachineIndex = 0;
                                                int lastDashIndex = machineInfo.MachineName.LastIndexOf("_");
                                                if (lastDashIndex > -1)
                                                {
                                                    if (int.TryParse(machineInfo.MachineName.Substring(lastDashIndex + 1), out int intVal) == true)
                                                    {
                                                        currentMachineIndex = intVal;
                                                    }
                                                }
                                                if (currentMachineIndex > 0)
                                                {
                                                    int nextMachineIndex = currentMachineIndex + 1;
                                                    string nextMachineName = machineInfo.MachineName.Substring(0, lastDashIndex) + "_" + nextMachineIndex;
                                                    bool createSuccessfull = await CreateMachine(actionConfig.ApiToken, actionConfig.AppName, machineConfig, createConfigJson, nextMachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                                    if (createSuccessfull ==true)
                                                    {
                                                        bool machinedStopped = await StopMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                                        if (machinedStopped == true)
                                                        {
                                                            await DeleteMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Skipped Updating Machine since cannot retrieve Previous MachineIndex for Machine [" + machineInfo.MachineName + "] for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");

                                                }

                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Skipped Updating Machine since MachineID not exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                        }
                                    }
                                }

                                if (actionConfig.ActionInterval > 0)
                                {
                                    await Task.Delay(actionConfig.ActionInterval).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    if (machineConfig.Action.Contains("Stop", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        foreach (RegionConfig itemRegion in actionConfig.RegionConfig)
                        {
                            if (itemRegion.Process == true)
                            {
                                Dictionary<string, MachineInfo> actionMachineInfoList = GetActionMachineInfoList(existingMachineList, itemRegion.RegionCode, actionConfig.AppName);

                                foreach (var item in actionMachineInfoList)
                                {
                                    MachineInfo machineInfo = item.Value;
                                    if ((machineInfo.Create == false) && (string.IsNullOrEmpty(machineInfo.MachineID) == false))
                                    {
                                        bool machinedStopped = await StopMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Skipped Stoping Machine since MachineID not exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                    }
                                }
                            }
                        }
                    }

                    if (machineConfig.Action.Contains("Start", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        foreach (RegionConfig itemRegion in actionConfig.RegionConfig)
                        {
                            if (itemRegion.Process == true)
                            {
                                Dictionary<string, MachineInfo> actionMachineInfoList = GetActionMachineInfoList(existingMachineList, itemRegion.RegionCode, actionConfig.AppName);

                                foreach (var item in actionMachineInfoList)
                                {
                                    MachineInfo machineInfo = item.Value;
                                    if ((machineInfo.Create == false) && (string.IsNullOrEmpty(machineInfo.MachineID) == false))
                                    {
                                        bool machinedStopped = await StartMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Skipped Start Machine since MachineID not exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                    }
                                }
                            }
                        }
                    }

                    if (machineConfig.Action.Contains("Delete", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        foreach (RegionConfig itemRegion in actionConfig.RegionConfig)
                        {
                            if (itemRegion.Process == true)
                            {
                                Dictionary<string, MachineInfo> actionMachineInfoList = GetActionMachineInfoList(existingMachineList, itemRegion.RegionCode, actionConfig.AppName);

                                foreach (var item in actionMachineInfoList)
                                {
                                    MachineInfo machineInfo = item.Value;
                                    if ((machineInfo.Create == false) && (string.IsNullOrEmpty(machineInfo.MachineID) == false))
                                    {
                                        await DeleteMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Skipped Deleting Machine since MachineID not exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                    }
                                }
                            }
                        }
                    }

                    if (machineConfig.Action.Contains("Destroy", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        foreach (RegionConfig itemRegion in actionConfig.RegionConfig)
                        {
                            if (itemRegion.Process == true)
                            {
                                Dictionary<string, MachineInfo> actionMachineInfoList = GetActionMachineInfoList(existingMachineList, itemRegion.RegionCode, actionConfig.AppName);

                                foreach (var item in actionMachineInfoList)
                                {
                                    MachineInfo machineInfo = item.Value;
                                    if ((machineInfo.Create == false) && (string.IsNullOrEmpty(machineInfo.MachineID) == false))
                                    {
                                        bool machinedStopped = await StopMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                        if (machinedStopped == true)
                                        {
                                            await DeleteMachine(actionConfig.ApiToken, actionConfig.AppName, machineInfo.MachineID, machineInfo.MachineName, machineInfo.MachineRegion, deployStartTimestamp).ConfigureAwait(false);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Skipped Destroying Machine since MachineID not exists for AppName [" + actionConfig.AppName + "] under Region [" + itemRegion.RegionCode + "]");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Machine CreateConfigJson and/or Machine UpdateConfigJson not found");
                }
            }
            return ret;
        }

        #endregion
    }
}