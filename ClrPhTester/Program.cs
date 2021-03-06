﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.ClrPh;
using System.IO;

namespace ClrPhTester
{
    class Program
    {
        static bool VerboseOutput = false;

        public static void VerboseWriteLine(string format, params object[] args)
        {
            if (VerboseOutput)
            {
                Console.WriteLine(format, args);
            }
        }

        public static void DumpKnownDlls()
        {
            VerboseWriteLine("[-] 64-bit KnownDlls : ");
            
            foreach (String KnownDll in Phlib.GetKnownDlls(false))
            {
                string System32Folder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                Console.WriteLine("  {0:s}\\{1:s}", System32Folder, KnownDll);
            }

            VerboseWriteLine("");

            VerboseWriteLine("[-] 32-bit KnownDlls : ");
            
            foreach (String KnownDll in Phlib.GetKnownDlls(true))
            {
                string SysWow64Folder = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                Console.WriteLine("  {0:s}\\{1:s}", SysWow64Folder, KnownDll);
            }


            VerboseWriteLine("");
        }

        public static void DumpApiSets()
        {
            VerboseWriteLine("[-] Api Sets Map : ");

            foreach (var ApiSetEntry in Phlib.GetApiSetSchema())
            {
                ApiSetTarget ApiSetImpl = ApiSetEntry.Value;
                string ApiSetName = ApiSetEntry.Key;
                string ApiSetImplStr = (ApiSetImpl.Count > 0) ? String.Join(",", ApiSetImpl.ToArray()) : "";

                Console.WriteLine("{0:s} -> [ {1:s} ]", ApiSetName, ApiSetImplStr);
            }

            VerboseWriteLine("");

        }

        

        public static void DumpManifest(PE Application)
        {
            String PeManifest = Application.GetManifest();
            VerboseWriteLine("[-] Manifest for file : {0}", Application.Filepath);

            if (PeManifest.Length == 0)
            {
                VerboseWriteLine("[x] No embedded pe manifest for file {0:s}", Application.Filepath);
                return;
            }

            try
            {
                // Use a memory stream to correctly handle BOM encoding for manifest resource
                using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(PeManifest)))
                {
                    XDocument XmlManifest = SxsManifest.ParseSxsManifest(stream);
                    Console.WriteLine(XmlManifest);
                }
                

            }
            catch (System.Xml.XmlException e)
            {
                Console.Error.WriteLine("[x] \"Malformed\" pe manifest for file {0:s} : {1:s}", Application.Filepath, PeManifest);
                Console.Error.WriteLine("[x] Exception : {0:s}", e.ToString());
            }
        }

        public static void DumpSxsEntries(PE Application)
        {
            SxsEntries SxsDependencies = SxsManifest.GetSxsEntries(Application);

            VerboseWriteLine("[-] sxs dependencies for executable : {0}", Application.Filepath);
            foreach (var entry in SxsDependencies)
            {
                if (entry.Path.Contains("???"))
                {
                    Console.WriteLine("  [x] {0:s} : {1:s}", entry.Name, entry.Path);
                }
                else
                {
                    Console.WriteLine("  [+] {0:s} : {1:s}", entry.Name, Path.GetFullPath(entry.Path));
                }
            }
        }


        public static void DumpExports(PE Pe)
        {
            List<PeExport> Exports = Pe.GetExports();
            VerboseWriteLine("[-] Export listing for file : {0}", Pe.Filepath);

            foreach (PeExport Export in Exports)
            {
                Console.WriteLine("Export {0:d} :", Export.Ordinal);
                Console.WriteLine("\t Name : {0:s}", Export.Name);
                Console.WriteLine("\t VA : 0x{0:X}", (int)Export.VirtualAddress);
                if (Export.ForwardedName.Length > 0)
                    Console.WriteLine("\t ForwardedName : {0:s}", Export.ForwardedName);
            }

            VerboseWriteLine("[-] Export listing done");
        }

        public static void DumpImports(PE Pe)
        {
            List<PeImportDll> Imports = Pe.GetImports();
            VerboseWriteLine("[-] Import listing for file : {0}", Pe.Filepath);

            foreach (PeImportDll DllImport in Imports)
            {
                Console.WriteLine("Import from module {0:s} :", DllImport.Name);

                foreach (PeImport Import in DllImport.ImportList)
                {
                    if (Import.ImportByOrdinal)
                    {
                        Console.Write("\t Ordinal_{0:d} ", Import.Ordinal);
                    }
                    else
                    {
                        Console.Write("\t Function {0:s}", Import.Name);
                    }
                    if (Import.DelayImport)
                        Console.WriteLine(" (Delay Import)");
                    else
                        Console.WriteLine("");
                }
            }

            VerboseWriteLine("[-] Import listing done");
        }

        public static void DumpUsage()
        {
            string Usage = String.Join(Environment.NewLine,
                "ClrPhTester.exe : COFF/PE dumper tool (kinda like dumpbin).",
                "",
                "Usage : ClrPhTester.exe [OPTIONS] FILE",
                "",
                "Options :",
                "  -h -help : display this help",
                "  -verbose : activate human centric output.",
                "  -apisets : display the ApiSet schema (api set dll -> host dll)",
                "  -knowndll : display all the known dlls (x86 and x64)",
                "  -manifest : display the FILE embedded manifest, if it exists.",
                "  -sxsentries : display all the FILE's sxs dependencies.",
                "  -imports : display the FILE imports",
                "  -exports : display the FILE exports"
            );

            Console.WriteLine(Usage);
        }


        static void Main(string[] args)
        { 

            Phlib.InitializePhLib();
            var ProgramArgs = ParseArgs(args);

            String FileName = null;
            if (ProgramArgs.ContainsKey("file"))
                FileName = ProgramArgs["file"];

            if (ProgramArgs.ContainsKey("-verbose"))
                VerboseOutput = true;

            // no need to load PE for those commands
            if ((args.Length == 0) || ProgramArgs.ContainsKey("-h") || ProgramArgs.ContainsKey("-help"))
            {
                DumpUsage();
                return;
            }

            if (ProgramArgs.ContainsKey("-knowndll"))
            {
                DumpKnownDlls();
                return;
            }

            if (ProgramArgs.ContainsKey("-apisets"))
            {
                DumpApiSets();
                return;
            }

            VerboseWriteLine("[-] Loading file {0:s} ", FileName);
            PE Pe = new PE(FileName);
            if (!Pe.LoadSuccessful)
            {
                Console.Error.WriteLine("[x] Could not load file {0:s} as a PE", FileName);
                return;
            }

            
            if (ProgramArgs.ContainsKey("-manifest"))
                DumpManifest(Pe);
            if (ProgramArgs.ContainsKey("-sxsentries"))
                DumpSxsEntries(Pe);
            if (ProgramArgs.ContainsKey("-imports"))
                DumpImports(Pe);
            if (ProgramArgs.ContainsKey("-exports"))
                DumpExports(Pe);


        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string s in args)
            {
                if (s.StartsWith("-", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dict.ContainsKey(s))
                        dict.Add(s, string.Empty);

                }
                else
                {
                    dict.Add("file", s);
                }
            }

            return dict;
        }
    }
}
