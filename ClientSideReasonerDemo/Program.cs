using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace UURAGE
{
    class Program
    {        
        static readonly string scenarioReasonerFileName = "ScenarioReasoner.cgi";
        static readonly string scenarioParserFileName = "ScenarioParser.cgi";
        static readonly string binDirectory = "bins";

        // Relative to the .exe in bin
        static readonly string cgiDirectory = ConfigurationManager.AppSettings["cgi_directory"];
        // Relative to the cgi directory
        static readonly string xmlDirectory = ConfigurationManager.AppSettings["xml_directory"];

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
            // This directory is relative to the cgi
            string scenarioBinPath = Path.Combine(binDirectory, scenarioName + ".bin");
            string scenarioXMLPath = Path.Combine(xmlDirectory, scenarioName + ".xml");

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

        static JArray DoStep(string scenarioID, JArray nextSteps)
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
                    bool endOfScenario = (bool)((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["end"])).Value;
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
                    
                return (JArray)((JArray)nextSteps[choice - 1])[3];
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
                    bool endOfScenario = (bool)((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["end"])).Value;
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
                    return (JArray)((JArray)nextSteps[choice])[3];
                }
                else
                {
                    return (JArray)((JArray)nextSteps[0])[3];
                }
            }
        }

        static void Main(string[] args)
        {
            // Provides support for unicode characters
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            // The unique identifier for the scenario obtained after parsing the scenario
            string scenarioID = "";
            // The initial scenario XML filename
            string scenarioName = "";
            bool loaded = false;
            
            Console.WriteLine("Please specify the name of the scenario XML without the .xml extension");
            scenarioName = Console.ReadLine();
            Console.WriteLine();

            Console.WriteLine("Loading scenario...");
            string output = PerformParserRequest(scenarioName);
            // Check if the parsing succeeded
            loaded = output.Contains(".bin");
            
            if (loaded)
            {
                // Get the start and end indices for the scenarioID
                int idStartIndex = output.IndexOf("bins\\") + 5;
                int idEndIndex = output.IndexOf(".bin");
                // Extract the scenarioID from the output
                scenarioID = output.Substring(idStartIndex, idEndIndex - idStartIndex);

                Console.WriteLine("Scenario successfully loaded");
            }
            else
                Console.WriteLine("Failed to load scenario");

            Console.WriteLine();

            if (loaded)
            {
                Console.WriteLine("Do you want to play through the scenario? (yes/no)");
                string answer = Console.ReadLine();
                Console.WriteLine();

                if (answer == "Yes" || answer == "Y" || answer == "yes" || answer == "y")
                {
                    JArray paramsID = new JArray((object)new JArray(scenarioID));
                    string initialDetails = ((JValue)((JArray)PerformReasonerRequest("examples", paramsID)[0])[1]).Value.ToString();
                    JArray nextState = new JArray(scenarioID, (new JArray()).ToString(), initialDetails, new JObject());

                    while (true)
                    {
                        // Call to the allfirsts service of the ScenarioReasoner
                        JArray nextSteps = (JArray)PerformReasonerRequest("scenarios.allfirsts", new JArray((object)nextState));

                        // Check if this statement ends the scenario or if there aren't any options left
                        bool endOfScenario = (bool)((JValue)(((JObject)JObject.Parse(((JValue)nextState[2]).Value.ToString())["statement"])["end"])).Value;
                        if (endOfScenario || nextSteps.Count == 0)
                        {
                            Console.WriteLine("End of the scenario!");
                            break;
                        }
                        else
                        {
                            nextState = DoStep(scenarioID, nextSteps);
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to close this console...");
            Console.ReadLine();
        }
    }
}
