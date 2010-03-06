using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System.Text.RegularExpressions;

namespace TfsConverter
{
    class Program
    {
        private static readonly Dictionary<string, string> Users =
            new Dictionary<string, string>() 
            {
            };

        /// <summary>
        /// {timestamp}|{user}|{action}|{filepath}|";
        /// </summary>
        public const string LogFormat = "{0}|{1}|{2}|{3}|";

        private static readonly IDictionary<ChangeType, string> AllowedTypes =
            new Dictionary<ChangeType, string>
            {
              {ChangeType.Add, "A"},
              {ChangeType.Edit, "M"},
              {ChangeType.Delete, "D"},
            };

        private static readonly DateTime UnixBaseDate = 
            new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();

        private static void Main(string[] args)
        {
            if (args.Length <= 2)
            {
                WriteHelp();
                return;
            }
            string tfsUrl = args[0];
            string projectUrl = args[1];
            if (tfsUrl.Trim().Length == 0)
            {
                Console.WriteLine("Missed Tfs url");
                return;
            }
            if (projectUrl.Trim().Length == 0)
            {
                Console.WriteLine("Missed ProjectUrl");
                return;
            }

            using (var server = new TeamFoundationServer(tfsUrl))
            {
                Console.WriteLine("Connecting to TFS");
                VersionControlServer source = (VersionControlServer)server.GetService(typeof(VersionControlServer));

                List<Changeset> combinedTree = new List<Changeset>();
                for (int i = 2; i < args.Length; ++i)
                {
                    string treeName = args[i].Trim();
                    if (treeName.Length == 0)
                    {
                        Console.WriteLine("Missed tree {0}", args[i]);
                        continue;
                    }
                    string treeUrl = String.Concat(projectUrl, "/", treeName);
                    Console.WriteLine("Search history for project url {0}", treeUrl);
                    List<Changeset> sourceTree = source.QueryHistory(treeUrl, VersionSpec.Latest, 0,
                        RecursionType.Full,
                        null,
                        null,
                        null,
                        int.MaxValue,
                        true,
                        false,
                        false).OfType<Changeset>().ToList<Changeset>();
                    EmitHistory(String.Format("{0}.log", treeName), sourceTree);
                    combinedTree = combinedTree.Concat<Changeset>(sourceTree as IEnumerable<Changeset>).ToList<Changeset>();
                }
                EmitHistory("All.log", combinedTree);
            }
        }

        private static void WriteHelp()
        {
            Console.WriteLine("Incorrect parameters");
            Console.WriteLine("Parameters: TfsConverter TfsUrl ProjectUrl Tree1 Tree2 ...");
            Console.WriteLine("Example https://SomeServ:8080/ $/Project Source Documents");
        }

        private static void EmitHistory(string filename, List<Changeset> history)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            using (var fileStream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var writer = new StreamWriter(fileStream))
                {
                    history.Sort(delegate(Changeset c1, Changeset c2) { return CompareChangesets(c1, c2); });
                    foreach (Changeset item in history)
                    {
                        long commitTime = ChangesetCommitTime(item);
                        string user = FormatUser(item.Owner);
                        Console.WriteLine("Processing changeset id = {0}. Comment {1}", item.ChangesetId,
                                          item.CreationDate);
                        foreach (Change change in item.Changes)
                        {
                            ChangeType changeType = change.ChangeType;
                            if (!AllowedTypes.Any(type => (type.Key & changeType) != 0))
                                continue;

                            KeyValuePair<ChangeType, string> code =
                                AllowedTypes.FirstOrDefault(type => (type.Key & changeType) != 0);
                            writer.WriteLine(LogFormat, commitTime, user, code.Value, change.Item.ServerItem);
                        }
                    }
                }
            }
        }

        private static string FormatUser(string user)
        {
            // Strip the company's domain name to make names more legible and also allow
            // us to use LefJab avatars downstream.
            Match match = new Regex(@"GLOBAL\\(.+)$").Match(user);
            string rawName = (match.Success) ? match.Groups[1].Value : user;
            return Users.ContainsKey(rawName) ? Users[rawName] : rawName;
        }

        private static long ChangesetCommitTime(Changeset item)
        {
            // Our Starteam import put checkin date/time in Starteam as a commit comment.
            // Extract it and use it when we can; else use the commit date/time in TFS.
            Match match = new Regex(@"(\d+/\d+/\d+ \d+:\d+:\d+ ..):.*$").Match(item.Comment);
            return DateTimeToUnix((match.Success) ? DateTime.Parse(match.Groups[1].Value) : item.CreationDate);
        }

        private static int CompareChangesets(Changeset c1, Changeset c2)
        {
            long t1 = ChangesetCommitTime(c1), t2 = ChangesetCommitTime(c2);
            if (t1 == t2)
                return 0;
            else if (t1 < t2)
                return -1;
            else
                return 1;
        }

        private static long DateTimeToUnix(DateTime dateTime)
        {
            //create Timespan by subtracting the value provided from the Unix Epoch
            return (long) (dateTime - UnixBaseDate).TotalSeconds;
        }
    }
}