using System;
using System.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace UURageAssetIntegration
{
    class Program
    {
        
        static readonly string scenarioReasonerFileName = "ScenarioReasoner.cgi";
        static readonly string scenarioParserFileName = "ScenarioParser.cgi";

        // Relative to the .exe in bin
        static readonly string cgiDirectory = ConfigurationManager.AppSettings["cgidir"];
        // Relative to the cgi directory
        static readonly string scenarioDirectory = ConfigurationManager.AppSettings["scenariodir"];

        static JToken PerformReasonerRequest(string method, JArray parameters)
        {
            JObject param = new JObject(
                new JProperty("method", method),
                new JProperty("params", parameters)
            );
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(cgiDirectory, scenarioReasonerFileName),
                WorkingDirectory = cgiDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            string input = Uri.EscapeDataString(param.ToString());
            startInfo.EnvironmentVariables.Add("REQUEST_METHOD", "GET");
            startInfo.EnvironmentVariables.Add("QUERY_STRING", "input=" + input);
            using (Process srProcess = Process.Start(startInfo))
            {
                string output = new StreamReader(srProcess.StandardOutput.BaseStream).ReadToEnd();
                JObject response = JObject.Parse(output.Split(new string[] { "\r\n\r\n" }, 2, StringSplitOptions.None)[1]);
                if (response["error"].Type != JTokenType.Null)
                {
                    throw new InvalidOperationException(((JValue)response["error"]).Value.ToString());
                }
                return response["result"];
            }
        }

        static string PerformParserRequest(string scenarioName)
        {
            // Relative to the cgi
            string scenarioBinPath = Path.Combine("bins", scenarioName + ".bin");
            string scenarioXMLPath = Path.Combine(scenarioDirectory, scenarioName + ".xml");

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(cgiDirectory, scenarioParserFileName),
                WorkingDirectory = cgiDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false                
            };

            startInfo.EnvironmentVariables.Add("REQUEST_METHOD", "GET");

            string query = "script_path=" + Uri.EscapeDataString(scenarioXMLPath) + "&" + "bin_path=" + Uri.EscapeDataString(scenarioBinPath);
            startInfo.EnvironmentVariables.Add("QUERY_STRING", query);

            using (Process spProcess = Process.Start(startInfo))
            {
                string output = new StreamReader(spProcess.StandardOutput.BaseStream).ReadToEnd();

                return output;
            }
        }

        static void DoStep(string scenarioID, JArray state)
        {
            JArray nextSteps = (JArray)PerformReasonerRequest("scenarios.allfirsts", new JArray((object)state));
            if (nextSteps.Count == 0)
            {
                Console.WriteLine("Scenario ended!");
            }
            else
            {
                string firstNextDetails = ((JValue)((JArray)((JArray)nextSteps[0])[3])[2]).Value.ToString();
                string firstNextType = ((JValue)(((JObject)JObject.Parse(firstNextDetails)["statement"])["type"])).Value.ToString();
                if (firstNextType == "player")
                {
                    Console.WriteLine("Choose the step that you want to take by giving the appropriate number");

                    int optionCounter = 1;
                    foreach (JToken nextStep in nextSteps)
                    {
                        JArray nextState = (JArray)((JArray)nextStep)[3];
                        string nextDetails = ((JValue)nextState[2]).Value.ToString();
                        string nextText = ((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["text"])).Value.ToString();
                        Console.WriteLine(optionCounter.ToString() + ". " + HttpUtility.HtmlDecode(nextText));
                        optionCounter++;
                    }

                    Console.WriteLine();

                    bool validChoice = false;
                    int choice = -1;
                    while (!validChoice)
                    {
                        if (!int.TryParse(Console.ReadLine(), out choice))
                            Console.WriteLine("Your choice is not a number");
                        else if (choice - 1 < 0 || choice - 1 >= nextSteps.Count)
                            Console.WriteLine("Your choice is out of the range of possible steps");
                        else
                            validChoice = true;

                        Console.WriteLine();
                        if (!validChoice)
                            Console.WriteLine("Please choose again");
                    }
                    
                    DoStep(scenarioID, (JArray)((JArray)nextSteps[choice - 1])[3]);
                }
                else
                {                    
                    int optionCounter = 0;
                    JArray nextState = null;
                    string nextDetails = null;
                    string nextText = null;

                    foreach (JToken nextStep in nextSteps)
                    {
                        nextState = (JArray)((JArray)nextStep)[3];
                        nextDetails = ((JValue)nextState[2]).Value.ToString();
                        nextText = ((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["text"])).Value.ToString();
                        Console.WriteLine(HttpUtility.HtmlDecode(nextText));
                        optionCounter++;
                    }
                    /* If there are multiple computer statements, i.e. more options for the counter, we randomly select one; 
                     * else we select the only option available.
                     * This section will be the integration part with INESC emotion detection asset
                     */ 
                    if (optionCounter > 1)
                    {
                        Random rnd = new Random();
                        Console.WriteLine("\nThe virtual character/computer has the above choices.");
                        int choice = rnd.Next(0, optionCounter);
                        Console.WriteLine("We randomly select: " + (choice + 1).ToString());
                        DoStep(scenarioID, (JArray)((JArray)nextSteps[choice])[3]);
                    }
                    else
                    {
                        DoStep(scenarioID, (JArray)((JArray)nextSteps[0])[3]);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            string scenarioID = "";
            string scenarioName = "";
            bool loaded = false;
            
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify the name of the scenario");
                scenarioName = Console.ReadLine();
                Console.WriteLine();

                Console.WriteLine("Loading scenario...");
                string output = PerformParserRequest(scenarioName);
                loaded = output.Contains(".bin");
                int idStartIndex = output.IndexOf("bins\\") + 5;
                int idEndIndex = output.IndexOf(".bin");

                scenarioID = output.Substring(idStartIndex, idEndIndex - idStartIndex);

                if (loaded)
                    Console.WriteLine("Scenario successfully loaded");
                else
                    Console.WriteLine("Failed to load scenario");

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("put the command line code in here");
            }

            if (loaded)
            {
                Console.WriteLine("Do you want to play through the scenario? (yes/no)");
                string answer = Console.ReadLine();
                Console.WriteLine();

                if (answer == "yes" || answer == "y")
                {
                    JArray paramsID = new JArray((object)new JArray(scenarioID));
                    string initialDetails = ((JValue)((JArray)PerformReasonerRequest("examples", paramsID)[0])[1]).Value.ToString();
                    DoStep(scenarioID, new JArray(scenarioID, (new JArray()).ToString(), initialDetails, new JObject()));
                    Console.ReadLine();
                }
            }
            else
                Console.ReadLine();
        }
    }
}
