using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        
        static readonly Encoding utf8EncodingWithoutBOM = new UTF8Encoding(false);

        static string QuoteProcessArgument(string argument)
        {
            // documented at
            // https://msdn.microsoft.com/en-us/library/system.diagnostics.processstartinfo.arguments.aspx
            return "\"" + argument.Replace("\"", "\"\"\"") + "\"";
        }

        static JToken PerformReasonerRequest(string method, JArray parameters)
        {
            JObject param = new JObject(
                new JProperty("method", method),
                new JProperty("params", parameters),
                new JProperty("encoding", "json")
            );
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(cgiDirectory, scenarioReasonerFileName),
                WorkingDirectory = cgiDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = utf8EncodingWithoutBOM,
                StandardErrorEncoding = utf8EncodingWithoutBOM,
                UseShellExecute = false,
                Arguments = "-r"
            };

            // Unfortunately, there is no StandardInputEncoding setting; the encoding for the
            // standard input stream is always taken from Console.InputEncoding in Process.Start.
            // We need to set that to the proper encoding (UTF8 *without* BOM) to ensure no BOM is
            // output, then reset it after starting the process.
            Encoding consoleInputEncoding = Console.InputEncoding;
            Console.InputEncoding = utf8EncodingWithoutBOM;
            using (Process srProcess = Process.Start(startInfo))
            {
                Console.InputEncoding = consoleInputEncoding;
                srProcess.StandardInput.WriteLine(param.ToString());
                srProcess.StandardInput.Close();
                string output = srProcess.StandardOutput.ReadToEnd();
                srProcess.WaitForExit();
                if (srProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException(srProcess.StandardError.ReadToEnd());
                }
                JObject response = JObject.Parse(output);
                if (response["error"].Type != JTokenType.Null)
                {
                    throw new InvalidOperationException(((JValue)response["error"]).Value.ToString());
                }
                return response["result"];
            }
        }

        static string ParseScenario(string scenarioName)
        {
            // This directory is relative to the cgi
            string scenarioBinPath = Path.Combine(binDirectory, scenarioName + ".bin");
            string scenarioXMLPath = Path.Combine(xmlDirectory, scenarioName + ".xml");

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(cgiDirectory, scenarioParserFileName),
                WorkingDirectory = cgiDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = utf8EncodingWithoutBOM,
                StandardErrorEncoding = utf8EncodingWithoutBOM,
                UseShellExecute = false
            };

            startInfo.Arguments = string.Join(" ", "-r",
                QuoteProcessArgument(scenarioXMLPath), QuoteProcessArgument(scenarioBinPath));

            using (Process spProcess = Process.Start(startInfo))
            {
                string output = spProcess.StandardOutput.ReadLine();
                spProcess.WaitForExit();
                if (spProcess.ExitCode == 0)
                {
                    return output;
                }
                else
                {
                    throw new InvalidOperationException(spProcess.StandardError.ReadToEnd());
                }
            }
        }

        static JArray DoStep(string scenarioID, JArray nextSteps)
        {
            JObject firstNextDetails = (JObject)((JArray)((JArray)nextSteps[0])[3])[2];
            string firstNextType = ((JValue)(((JObject)firstNextDetails["statement"])["type"])).Value.ToString();
            if (firstNextType == "player")
            {
                Console.WriteLine("Choose the step that you want to take by giving the appropriate number");

                int optionCounter = 1;
                foreach (JToken nextStep in nextSteps)
                {
                    JArray nextState = (JArray)((JArray)nextStep)[3];
                    JObject nextDetails = (JObject)(nextState[2]);
                    string nextText = ((JValue)((nextDetails["statement"])["text"])).Value.ToString();
                    Console.WriteLine(optionCounter.ToString() + ". " + nextText);
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
                foreach (JToken nextStep in nextSteps)
                {
                    JArray nextState = (JArray)((JArray)nextStep)[3];
                    JObject nextDetails = (JObject)nextState[2];
                    string nextText = ((JValue)(((JObject)nextDetails["statement"])["text"])).Value.ToString();
                    Console.WriteLine(nextText);
                }

                /* If there are multiple computer statements, i.e. more options for the counter, we randomly select one; 
                 * else we select the only option available.
                 * This section will be the integration part with INESC emotion detection asset
                 */
                if (nextSteps.Count > 1)
                {
                    Random rnd = new Random();
                    Console.WriteLine("\nThe virtual character/computer has the above choices.");
                    int choice = rnd.Next(0, nextSteps.Count);
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
            // Provide support for Unicode characters
            // If the input encoding is already UTF-8, the Console is probably set up correctly.
            // If not, we need to set a Unicode-covering encoding.
            // Windows appears to prefer Encoding.Unicode.
            if (Console.InputEncoding.WebName != Encoding.UTF8.WebName)
            {
                Console.InputEncoding = Encoding.Unicode;
                Console.OutputEncoding = Encoding.Unicode;
            }
            
            Console.WriteLine("Please specify the name of the scenario XML without the .xml extension");
            // The initial scenario XML filename
            string scenarioName = Console.ReadLine();
            Console.WriteLine();

            Console.WriteLine("Loading scenario...");
            // The unique identifier for the scenario obtained after parsing the scenario
            string scenarioID = ParseScenario(scenarioName);
            Console.WriteLine("Scenario successfully loaded");
            Console.WriteLine();
            
            Console.WriteLine("Do you want to play through the scenario? (yes/no)");
            string answer = Console.ReadLine();
            Console.WriteLine();

            if (answer == "Yes" || answer == "Y" || answer == "yes" || answer == "y")
            {
                JArray paramsID = new JArray((object)new JArray(scenarioID));
                JObject initialDetails = (JObject)((JArray)PerformReasonerRequest("examples", paramsID)[0])[1];
                JArray nextState = new JArray(scenarioID, (new JArray()).ToString(), initialDetails, new JObject());

                while (true)
                {
                    // Call to the allfirsts service of the ScenarioReasoner
                    JArray nextSteps = (JArray)PerformReasonerRequest("scenarios.allfirsts", new JArray((object)nextState));

                    // Check if there aren't any options left
                    if (nextSteps.Count == 0)
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

            Console.WriteLine();
            Console.WriteLine("Press any key to close this console...");
            Console.ReadKey();
        }
    }
}
