using System;
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
        static string scenarioReasonerDirectory = "";
        static string scenarioReasonerFileName = "ScenarioReasoner.cgi";

        static JToken PerformSRRequest(string method, JArray parameters)
        {
            JObject param = new JObject(
                new JProperty("method", method),
                new JProperty("params", parameters)
            );
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(scenarioReasonerDirectory, scenarioReasonerFileName),
                WorkingDirectory = scenarioReasonerDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.EnvironmentVariables.Add("REQUEST_METHOD", "GET");
            startInfo.EnvironmentVariables.Add("QUERY_STRING", "input=" + Uri.EscapeDataString(Encoding.GetEncoding(28591).GetString(Encoding.UTF8.GetBytes(param.ToString()))));
            using (Process srProcess = Process.Start(startInfo))
            {
                string output = Encoding.UTF8.GetString(Encoding.GetEncoding(28591).GetBytes(new StreamReader(srProcess.StandardOutput.BaseStream, Encoding.UTF8).ReadToEnd()));
                JObject response = JObject.Parse(output.Split(new string[] { "\r\n\r\n" }, 2, StringSplitOptions.None)[1]);
                if (response["error"].Type != JTokenType.Null)
                {
                    throw new InvalidOperationException(((JValue)response["error"]).Value.ToString());
                }
                return response["result"];
            }
        }

        static void DoOneStep(string scenarioID, JArray state)
        {
            JArray nexts = (JArray)PerformSRRequest("scenarios.allfirsts", new JArray((object)state));
            if (nexts.Count == 0)
            {
                Console.WriteLine("Scenario ended!");
            }
            else
            {
                string firstNextDetails = ((JValue)((JArray)((JArray)nexts[0])[3])[2]).Value.ToString();
                string firstNextType = ((JValue)(((JObject)JObject.Parse(firstNextDetails)["statement"])["type"])).Value.ToString();
                if (firstNextType == "player")
                {
                    int i = 1;
                    foreach (JToken next in nexts)
                    {
                        JArray nextState = (JArray)((JArray)next)[3];
                        string nextDetails = ((JValue)nextState[2]).Value.ToString();
                        string nextText = ((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["text"])).Value.ToString();
                        Console.WriteLine(i.ToString() + ". " + HttpUtility.HtmlDecode(nextText));
                        i++;
                    }
                    int choice = int.Parse(Console.ReadLine()) - 1;
                    DoOneStep(scenarioID, (JArray)((JArray)nexts[choice])[3]);
                }
                else
                {
                    JArray nextState = (JArray)((JArray)nexts[0])[3];
                    string nextDetails = ((JValue)nextState[2]).Value.ToString();
                    string nextText = ((JValue)(((JObject)JObject.Parse(nextDetails)["statement"])["text"])).Value.ToString();
                    Console.WriteLine(HttpUtility.HtmlDecode(nextText));
                    DoOneStep(scenarioID, nextState);
                }
            }
        }

        static void Main(string[] args)
        {
            //string scenarioID = args[0];
            string scenarioID = "scenarios.295";
            string initialDetails =
                ((JValue)((JArray)PerformSRRequest("examples", new JArray((object)new JArray(scenarioID)))[0])[1]).Value.ToString();
            DoOneStep(scenarioID, new JArray(scenarioID, (new JArray()).ToString(), initialDetails, new JObject()));
            Console.ReadLine();
        }
    }
}
