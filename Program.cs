// Copyright(c) 2018 Johan Lindvall
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace HostsFileEditor
{
    using System;
    using System.ComponentModel;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Principal;

    class Program
    {
        private static readonly string Filename = @"c:\windows\system32\drivers\etc\hosts";

        static void Main(string[] args)
        {
            var current = Read().ToList();

            if (args.Length == 0)
            {
                ListContents(current);
            }

            for (var i = 0; i < args.Length; ++i)
            {
                var arg = args[i];
                var isLastArg = i == args.Length - 1;

                if (string.Equals("list", arg, StringComparison.OrdinalIgnoreCase))
                {
                    ListContents(current);
                }
                else if (string.Equals("remove", arg, StringComparison.OrdinalIgnoreCase) && !isLastArg)
                {
                    while (i < args.Length - 1)
                    {
                        ++i;
                        current = current.Where(line => IsCommentOrWhitespace(line) || !line.Split(' ').Any(part => part.Equals(args[i], StringComparison.OrdinalIgnoreCase))).ToList();
                    }

                    Write(current);
                }
                else if (string.Equals("add", arg, StringComparison.OrdinalIgnoreCase) && !isLastArg)
                {
                    while (i < args.Length - 2)
                    {
                        current.Add(args[i + 1] + " " + args[i + 2]);
                        i += 2;
                    }

                    Write(current);
                }
                else if (string.Equals("block", arg, StringComparison.OrdinalIgnoreCase) && !isLastArg)
                {
                    while (i < args.Length - 1)
                    {
                        current.Add("127.0.0.1 " + args[++i]);
                    }

                    Write(current);
                }
                else
                {
                    Usage();
                }
            }
        }

        private static void ListContents(List<string> current)
        {
            var anything = false;
            foreach (var line in current.Where(l => !IsCommentOrWhitespace(l)))
            {
                Console.WriteLine(line);
                anything = true;
            }

            if (!anything)
            {
                Console.WriteLine("Hosts file is empty.");
            }
        }

        private static void Usage()
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            Console.WriteLine("Usage: ");
            Console.WriteLine("");
            Console.WriteLine($@"{processName} [list] - list entries in hosts file");
            Console.WriteLine($@"{processName} remove [name] - removes name from hosts file");
            Console.WriteLine($@"{processName} add [ip name] - adds ip and name to hosts file");
            Console.WriteLine($@"{processName} block [name] - adds 127.0.0.1 and name to hosts file");
        }

        private static bool IsCommentOrWhitespace(string line) => line.StartsWith("#") || string.IsNullOrEmpty(line.Trim());

        static IEnumerable<string> Read()
        {
            using (var reader = new StreamReader(Filename))
            {
                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null)
                    {
                        yield break;
                    }

                    yield return line;
                }
            }
        }

        private static bool IsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        private static void RelaunchAsAdministrator() => Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, Environment.CommandLine)
        {
            Verb = "runas"
        });

        static void Write(IList<string> lines)
        {
            if (CheckDiffs(Read(), lines))
            {
                if (IsAdministrator())
                {
                    File.WriteAllText(Filename, string.Join(Environment.NewLine, lines));
                }
                else
                {
                    try
                    {
                        RelaunchAsAdministrator();
                    }
                    catch (Win32Exception)
                    {
                        // Ignore
                    }
                }
            }
        }

        static bool CheckDiffs(IEnumerable<string> oldContents, IEnumerable<string> newContents)
        {
            var hasDiffs = false;
            using (var oldEnumerator = oldContents.GetEnumerator())
            {
                using (var newEnumerator = newContents.GetEnumerator())
                {
                    var oldValid = oldEnumerator.MoveNext();
                    var newValid = newEnumerator.MoveNext();

                    while (oldValid || newValid)
                    {
                        if (oldValid)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            if (newValid && oldEnumerator.Current.Equals(newEnumerator.Current, StringComparison.OrdinalIgnoreCase))
                            {
                                // Equals
                                oldValid = oldEnumerator.MoveNext();
                                newValid = newEnumerator.MoveNext();
                            }
                            else
                            {
                                Console.WriteLine($@"Removing {oldEnumerator.Current}.");
                                hasDiffs = true;
                                oldValid = oldEnumerator.MoveNext();
                            }
                        }
                        else
                        {
                            Console.WriteLine($@"Adding {newEnumerator.Current}.");
                            hasDiffs = true;
                            newValid = newEnumerator.MoveNext();
                        }
                    }
                }
            }

            if (!hasDiffs)
            {
                Console.WriteLine("No changes detected.");
            }

            return hasDiffs;
        }
    }
}
