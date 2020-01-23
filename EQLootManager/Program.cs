using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EQLootManager
{
    class Program
    { 
        static string EQPath = "";
        static WebhookCaller WHCaller;

        static void Main(string[] args)
        {
            
            if (EQPath == "")
                GetEQPath();

            PromptForWebhookURL();
            Task.Run(ParseFile);

            while (true)
            {
                string input = Console.ReadLine();
                if (input == "1")
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\EQLootManager", "WebhookURL", "");
                    System.Diagnostics.Process.Start(System.AppDomain.CurrentDomain.FriendlyName);
                    Environment.Exit(0);
                }
                else if (input == "q")
                {
                    Environment.Exit(0);
                }
                else if (input == "2")
                {
                    EQPathManualEntry();
                    System.Diagnostics.Process.Start(System.AppDomain.CurrentDomain.FriendlyName);
                    Environment.Exit(0);
                }
            }
        }

        private static void DisplayMenu()
        {
            Console.Clear();
            Console.WriteLine("EQLootManager is now active with {0} as the EverQuest directory.", EQPath);
            Console.WriteLine();
            Console.WriteLine("HOW TO USE (type the commands in game):");
            Console.WriteLine("Start a basic bid: /note startbids ITEMNAME");
            Console.WriteLine("Start a bid with 2x quantity: /note startbids 2x ITEMNAME");
            Console.WriteLine("Start a bid with custom countdown: /note startbids ITEMNAME 6");
            Console.WriteLine("Cancel a bid: /note cancel ITEMNAME");
            Console.WriteLine("NOTE: You can link the item rather than typing it out.");
            Console.WriteLine("NOTE: You do not need to type '.dkp' to submit the command.");
            Console.WriteLine("NOTE: To quickly submit a new item with no options, you can simply type /note, link the item, and press enter.");
            Console.WriteLine();
            Console.WriteLine("To change the Webhook URL, type 1 and press ENTER");
            Console.WriteLine("To change the EverQuest install directory, type 2 and press ENTER");
            Console.WriteLine("Close this window or type q and press ENTER to exit.");
        }
        private static void PromptForWebhookURL()
        {
            string url = "";
            var regValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\EQLootManager", "WebhookURL", "0");

            if (regValue != null)
                url = regValue.ToString();

            if (url == "" || url == "0")
            {
                Console.WriteLine("Paste the Webhook URL and press ENTER. This will be given to you by a Discord admin.");

                string input = Console.ReadLine();
                if (input.StartsWith(@"https://discordapp.com/api/webhooks/"))
                {
                    url = input;
                }
                else
                {
                    Console.WriteLine("URL appears to be invalid.");
                    PromptForWebhookURL();
                }
            }

            WHCaller = new WebhookCaller(url);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\EQLootManager", "WebhookURL", url);
            DisplayMenu();
        }

        private static void EQPathManualEntry()
        {
            Console.WriteLine("Enter the EverQuest directory now.");
            Console.WriteLine(@"Example: C:\EverQuest");
            string input = Console.ReadLine();
            if (input != "")
            {
                var eqDir = new DirectoryInfo(input);
                if (File.Exists(eqDir.ToString() + @"\eqgame.exe"))
                {
                    EQPath = eqDir.FullName + @"\";
                    EQPath = EQPath.Replace(@"\\", @"\");
                    SaveManualEQPath(EQPath + "eqgame.exe");
                    return;
                }
                else
                {
                    Console.WriteLine("Could not find EverQuest location at {0}.", eqDir.FullName);
                }
            }
            EQPathManualEntry();
        }

        private static void SaveManualEQPath(string path)
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\EQLootManager", "EQPath", path);
        }

        private static void GetEQPath()
        {
            string path = "";
            var manualEQPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\EQLootManager", "EQPath", "0");
            if (manualEQPath != null)
                path = manualEQPath.ToString();
            if (path == "" || path == "0")
            {
                var appPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\LaunchPad.exe", "", "0");
                if (appPath != null)
                    path = appPath.ToString();

            }
            if (path == "" || path == "0")
            {
                Console.WriteLine("Could not find EverQuest location.");
                EQPathManualEntry();
            }
            else
            {
                var eqDir = new DirectoryInfo(path).Parent;
                if (!File.Exists(eqDir.FullName + @"\eqgame.exe"))
                {
                    Console.WriteLine("Could not find EverQuest location at {0}.", eqDir.FullName);
                    EQPathManualEntry();
                }
                Console.WriteLine("EverQuest installation found at {0}.", eqDir.FullName);
                EQPath = eqDir.FullName + @"\";
                EQPath = EQPath.Replace(@"\\", @"\");
            }
        }


        public static void ParseFile()
        {
            string path = EQPath + "notes.txt";
            // if the notes.txt file doesn't exist yet, we'll create it.
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }
            
            Regex regex = new Regex(@"(startbids\s|startbid\s|startauction\s|cancel\s|)(?:(?:\d+)?x?\s?|)(?:(.*?)(\s\d+|$))", RegexOptions.IgnoreCase);
            using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {

                long lastMaxOffset = reader.BaseStream.Length;
                
                while (true)
                {
                    System.Threading.Thread.Sleep(100);

                    //if the file size has not changed, idle
                    if (reader.BaseStream.Length == lastMaxOffset)
                        continue;

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        string lineClean = line.Substring(line.IndexOf(']') + 2);
                        lineClean = lineClean.Replace(".dkp", "").Replace("\"","").Trim();
                        Match match = regex.Match(lineClean);
                        if (match.Success)
                        {
                            string itemName = match.Groups[2].ToString().Trim();
                            Regex delimiters = new Regex(@"(!|;)");
                            
                            string[] multipleItems = delimiters.Split(itemName);
                            if (multipleItems.Length > 0)
                            {
                                
                                string multiItemCombined = "";
                                foreach (var item in multipleItems)
                                {
                                    if (!delimiters.Match(item).Success && item != "")
                                    {
                                        multiItemCombined = String.Format(@"{0} ""{1}""", multiItemCombined, item.Trim());



                                        // REMOVE SECTION BELOW AFTER MULTIITEM IS IMPLEMENTED IN DISCIPLE BOT, workaround to submit multiple items
                                        string lineToSend = "";
                                        lineToSend = lineClean.Replace(itemName, String.Format(@"""{0}""", item.Trim()));
                                        if (match.Groups[1].ToString() == "")
                                        {
                                            lineToSend = "startbids " + lineToSend.TrimStart();
                                        }
                                        lineToSend = ".dkp " + lineToSend;
                                        WHCaller.SendHook(lineToSend.Trim());





                                        // REMOVE SECTION ABOVE AFTER MULTIITEM IS IMPLEMENTED IN DISCIPLE BOT
                                    }
                                }
                                lineClean = lineClean.Replace(itemName, multiItemCombined.Trim());
                            }
                            else
                            { // TEMP, REMOVE AFTER MULTIITEM IS IMPLEMENTED IN DISCIPLE BOT
                                lineClean = lineClean.Replace(itemName, String.Format(@"""" + "{0}" + @"""", itemName));
                                if (match.Groups[1].ToString() == "")
                                {
                                    lineClean = "startbids " + lineClean.TrimStart();
                                }
                                lineClean = ".dkp " + lineClean;
                                WHCaller.SendHook(lineClean.Trim());
                            } // TEMP, REMOVE AFTER MULTIITEM IS IMPLEMENTED IN DISCIPLE BOT
                        }
                    }
                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;
                }
            }
        }

        
    }
}
