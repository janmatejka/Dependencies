﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using System.Diagnostics;

using NDesk.Options;
using Newtonsoft.Json;
using Dependencies.ClrPh;
using System.Text.RegularExpressions;
using System.Text;

namespace Dependencies
{

    interface IPrettyPrintable
    {
        void PrettyPrint(bool bare);
    }

    /// <summary>
    /// Printable KnownDlls object
    /// </summary>
    class NtKnownDlls : IPrettyPrintable
    {
        public NtKnownDlls()
        {
            x64 = Phlib.GetKnownDlls(false);
            x86 = Phlib.GetKnownDlls(true);
        }

        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] 64-bit KnownDlls : ");

            foreach (String KnownDll in this.x64)
            {
                string System32Folder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                Console.WriteLine("  {0:s}\\{1:s}", System32Folder, KnownDll);
            }

            Console.WriteLine("");

            Console.WriteLine("[-] 32-bit KnownDlls : ");

            foreach (String KnownDll in this.x86)
            {
                string SysWow64Folder = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                Console.WriteLine("  {0:s}\\{1:s}", SysWow64Folder, KnownDll);
            }


            Console.WriteLine("");
        }

        public List<String> x64;
        public List<String> x86;
    }

    /// <summary>
    /// Printable ApiSet schema object
    /// </summary>
    class NtApiSet : IPrettyPrintable
    {
        public NtApiSet()
        {
            Schema = Phlib.GetApiSetSchema();
        }

        public NtApiSet(PE ApiSetSchemaDll)
        {
            Schema = ApiSetSchemaDll.GetApiSetSchema();
        }

        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] Api Sets Map : ");

            foreach (var ApiSetEntry in this.Schema.GetAll())
            {
                ApiSetTarget ApiSetImpl = ApiSetEntry.Value;
                string ApiSetName = ApiSetEntry.Key;
                string ApiSetImplStr = (ApiSetImpl.Count > 0) ? String.Join(",", ApiSetImpl.ToArray()) : "";

                Console.WriteLine("{0:s} -> [ {1:s} ]", ApiSetName, ApiSetImplStr);
            }

            Console.WriteLine("");
        }

        public ApiSetSchema Schema;
    }


    class PEManifest : IPrettyPrintable
    {

        public PEManifest(PE _Application)
        {
            Application = _Application;
            Manifest = Application.GetManifest();
            XmlManifest = null;
            Exception = "";

            if (Manifest.Length != 0)
            {
                try
                {
                    // Use a memory stream to correctly handle BOM encoding for manifest resource
                    using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(Manifest)))
                    {
                        XmlManifest = SxsManifest.ParseSxsManifest(stream);
                    }


                }
                catch (System.Xml.XmlException e)
                {
                    //Console.Error.WriteLine("[x] \"Malformed\" pe manifest for file {0:s} : {1:s}", Application.Filepath, PeManifest);
                    //Console.Error.WriteLine("[x] Exception : {0:s}", e.ToString());
                    XmlManifest = null;
                    Exception = e.ToString();
                }
            }
        }


        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] Manifest for file : {0}", Application.Filepath);

            if (Manifest.Length == 0)
            {
                Console.WriteLine("[x] No embedded pe manifest for file {0:s}", Application.Filepath);
                return;
            }

            if (Exception.Length != 0)
            {
                Console.Error.WriteLine("[x] \"Malformed\" pe manifest for file {0:s} : {1:s}", Application.Filepath, Manifest);
                Console.Error.WriteLine("[x] Exception : {0:s}", Exception);
                return;
            }

            Console.WriteLine(XmlManifest);
        }

        public string Manifest;
        public XDocument XmlManifest;

        // stays private in order not end up in the json output
        private PE Application;
        private string Exception;
    }

    class PEImports : IPrettyPrintable
    {
        public PEImports(PE _Application)
        {
            Application = _Application;
            Imports = Application.GetImports();
        }

        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] Import listing for file : {0}", Application.Filepath);

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

            Console.WriteLine("[-] Import listing done");
        }

        public List<PeImportDll> Imports;
        private PE Application;
    }

    class PEExports : IPrettyPrintable
    {
        public PEExports(PE _Application)
        {
            Application = _Application;
            Exports = Application.GetExports();
        }

        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] Export listing for file : {0}", Application.Filepath);

            foreach (PeExport Export in Exports)
            {
                Console.WriteLine("Export {0:d} :", Export.Ordinal);
                Console.WriteLine("\t Name : {0:s}", Export.Name);
                Console.WriteLine("\t VA : 0x{0:X}", (int)Export.VirtualAddress);
                if (Export.ForwardedName.Length > 0)
                    Console.WriteLine("\t ForwardedName : {0:s}", Export.ForwardedName);
            }

            Console.WriteLine("[-] Export listing done");
        }

        public List<PeExport> Exports;
        private PE Application;
    }


    class SxsDependencies : IPrettyPrintable
    {
        public SxsDependencies(PE _Application)
        {
            Application = _Application;
            SxS = SxsManifest.GetSxsEntries(Application);
        }

        public void PrettyPrint(bool bare)
        {
            Console.WriteLine("[-] sxs dependencies for executable : {0}", Application.Filepath);
            foreach (var entry in SxS)
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

        public SxsEntries SxS;
        private PE Application;

    }


    // Basic custom exception used to be able to differentiate between a "native" exception
    // and one that has been already catched, processed and rethrown
    public class RethrownException : Exception
    {
        public RethrownException(Exception e)
        : base(e.Message, e.InnerException)
        {
        }

    }


    class PeDependencyItem : IPrettyPrintable
    {

        public PeDependencyItem(PeDependencies _Root, string _ModuleName, string ModuleFilepath, ModuleSearchStrategy Strategy, int Level)
        {
            Action action = () =>
            {
                Root = _Root;
                ModuleName = _ModuleName;


                Imports = new List<PeImportDll>();
                Filepath = ModuleFilepath;
                SearchStrategy = Strategy;
                RecursionLevel = Level;

                DependenciesResolved = false;
                FullDependencies = new List<PeDependencyItem>();
                ResolvedImports = new List<PeDependencyItem>();
            };

            SafeExecutor(action);
        }

        public void LoadPe()
        {
            Action action = () =>
            {
                if (Filepath != null)
                {
                    PE Module = BinaryCache.LoadPe(Filepath);
                    Imports = Module.GetImports();
                }
                else
                {
                    //Module = null;
                }
            };

            SafeExecutor(action);
        }

        public void ResolveDependencies()
        {
            Action action = () =>
            {
                if (DependenciesResolved)
                {
                    return;
                }

                List<PeDependencyItem> NewDependencies = new List<PeDependencyItem>();

                foreach (PeImportDll DllImport in Imports)
                {
                    string ModuleFilepath = null;
                    ModuleSearchStrategy Strategy;


                    // Find Dll in "paths"
                    Tuple<ModuleSearchStrategy, PE> ResolvedModule = Root.ResolveModule(DllImport.Name);
                    Strategy = ResolvedModule.Item1;

                    if (Strategy != ModuleSearchStrategy.NOT_FOUND)
                    {
                        ModuleFilepath = ResolvedModule.Item2?.Filepath;
                    }



                    bool IsAlreadyCached = Root.isModuleCached(DllImport.Name, ModuleFilepath);
                    PeDependencyItem DependencyItem = Root.GetModuleItem(DllImport.Name, ModuleFilepath, Strategy, RecursionLevel + 1);

                    // do not add twice the same imported module
                    if (ResolvedImports.Find(ri => ri.ModuleName == DllImport.Name) == null)
                    {
                        ResolvedImports.Add(DependencyItem);
                    }

                    // Do not process twice a dependency. It will be displayed only once
                    if (!IsAlreadyCached)
                    {
                        Debug.WriteLine("[{0:d}] [{1:s}] Adding dep {2:s}", RecursionLevel, ModuleName, ModuleFilepath);
                        NewDependencies.Add(DependencyItem);
                    }

                    FullDependencies.Add(DependencyItem);

                }

                DependenciesResolved = true;
                if ((Root.MaxRecursion > 0) && ((RecursionLevel + 1) >= Root.MaxRecursion))
                {
                    return;
                }


                // Recursively resolve dependencies
                foreach (var Dep in NewDependencies)
                {
                    Dep.LoadPe();
                    Dep.ResolveDependencies();
                }
            };

            SafeExecutor(action);
        }

        public bool IsNewModule()
        {
            return Root.VisitModule(this.ModuleName, this.Filepath);
        }

        public void PrettyPrint(bool bare)
        {
            string Tabs = string.Concat(Enumerable.Repeat("|  ", RecursionLevel));
            Console.WriteLine("{0:s}├ {1:s} ({2:s}) : {3:s} ", Tabs, ModuleName, SearchStrategy.ToString(), Filepath);

            foreach (var Dep in ResolvedImports)
            {
                bool NeverSeenModule = Dep.IsNewModule();
                Dep.RecursionLevel = RecursionLevel + 1;

                if (NeverSeenModule)
                {
                    Dep.PrettyPrint(bare);
                }
                else
                {
                    Dep.BasicPrettyPrint(bare);
                }

            }
        }

        public void BasicPrettyPrint(bool bare, int? OverrideRecursionLevel = null)
        {
            int localRecursionLevel = RecursionLevel;
            if (OverrideRecursionLevel != null)
            {
                localRecursionLevel = (int)OverrideRecursionLevel;
            }

            string Tabs = string.Concat(Enumerable.Repeat("|  ", localRecursionLevel));
            Console.WriteLine("{0:s}├ {1:s} ({2:s}) : {3:s} ", Tabs, ModuleName, SearchStrategy.ToString(), Filepath);
        }

        private void SafeExecutor(Action action)
        {
            SafeExecutor(() => { action(); return 0; });
        }

        private T SafeExecutor<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (RethrownException rex)
            {
                Console.Error.WriteLine(" - \"{0:s}\"", Filepath);
                throw rex;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[!] Unhandled exception occured while processing \"{1:s}\"", RecursionLevel, Filepath);
                Console.Error.WriteLine("Stacktrace:\n{0:s}\n", ex.StackTrace);
                Console.Error.WriteLine("Modules backtrace:");
                throw new RethrownException(ex);
            }
            finally
            {
                //

            }

            // return default(T);
        }

        // Json exportable
        public string ModuleName;
        public string Filepath;
        public ModuleSearchStrategy SearchStrategy;
        public List<PeDependencyItem> Dependencies
        {

            get { return IsNewModule() ? FullDependencies : new List<PeDependencyItem>(); }
        }

        // not Json exportable
        protected List<PeDependencyItem> FullDependencies;
        protected List<PeDependencyItem> ResolvedImports;
        protected List<PeImportDll> Imports;
        protected PeDependencies Root;
        protected int RecursionLevel;

        private bool DependenciesResolved;
    }


    class ModuleCacheKey : Tuple<string, string>
    {
        public ModuleCacheKey(string Name, string Filepath)
        : base(Name, Filepath)
        {
        }
    }

    class ModuleEntries : Dictionary<ModuleCacheKey, PeDependencyItem>, IPrettyPrintable
    {
        public void PrettyPrint(bool bare)
        {
            if (bare)
            {
                var sb = new StringBuilder();
                foreach (var item in this.Values.OrderBy(module => module.ModuleName))
                {
                    sb.Append('\t');
                    sb.Append(item.ModuleName);
                }
                Console.WriteLine(sb.ToString());
            }
            else
            {
                foreach (var item in this.Values.OrderBy(module => module.SearchStrategy))
                {
                    Console.WriteLine("[{0:s}] {1:s} : {2:s} ", item.SearchStrategy.ToString(), item.ModuleName, item.Filepath);
                }
            }

        }
    }

    class PeDependencies : IPrettyPrintable
    {
        public PeDependencies(PE Application, int recursion_depth)
        {
            string RootFilename = Path.GetFileName(Application.Filepath);

            RootPe = Application;
            SxsEntriesCache = SxsManifest.GetSxsEntries(RootPe);
            ModulesCache = new ModuleEntries();
            MaxRecursion = recursion_depth;

            ModulesVisited = new Dictionary<ModuleCacheKey, bool>();

            Root = GetModuleItem(RootFilename, Application.Filepath, ModuleSearchStrategy.ROOT, 0);
            Root.LoadPe();
            Root.ResolveDependencies();
        }

        public Tuple<ModuleSearchStrategy, PE> ResolveModule(string ModuleName)
        {
            return BinaryCache.ResolveModule(
                RootPe,
                ModuleName /*DllImport.Name*/
            );
        }

        public bool isModuleCached(string ModuleName, string ModuleFilepath)
        {
            // Do not process twice the same item
            ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            return ModulesCache.ContainsKey(ModuleKey);
        }

        public PeDependencyItem GetModuleItem(string ModuleName, string ModuleFilepath, ModuleSearchStrategy SearchStrategy, int RecursionLevel)
        {
            // Do not process twice the same item
            ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            if (!ModulesCache.ContainsKey(ModuleKey))
            {
                ModulesCache[ModuleKey] = new PeDependencyItem(this, ModuleName, ModuleFilepath, SearchStrategy, RecursionLevel);
            }

            return ModulesCache[ModuleKey];
        }

        public void PrettyPrint(bool bare)
        {
            ModulesVisited = new Dictionary<ModuleCacheKey, bool>();
            Root.PrettyPrint(bare);
        }

        public bool VisitModule(string ModuleName, string ModuleFilepath)
        {
            //ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            ModuleCacheKey ModuleKey = new ModuleCacheKey("", ModuleFilepath);

            // do not visit recursively the same node (in order to prevent stack overflow)
            if (ModulesVisited.ContainsKey(ModuleKey))
            {
                return false;
            }

            ModulesVisited[ModuleKey] = true;
            return true;
        }



        public ModuleEntries GetModules
        {
            get { return ModulesCache; }
        }

        public PeDependencyItem Root;
        public int MaxRecursion;

        private PE RootPe;
        private SxsEntries SxsEntriesCache;
        private ModuleEntries ModulesCache;
        private Dictionary<ModuleCacheKey, bool> ModulesVisited;
    }



    class Program
    {
        public class PrettyPrinter
        {
            public PrettyPrinter(bool bare)
            {
                _bare = bare;
            }
            private bool _bare;
            public void Print(IPrettyPrintable obj)
            {
                obj.PrettyPrint(_bare);
            }
        }

        public static void JsonPrinter(IPrettyPrintable obj)
        {
            JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                //PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            };

            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented, Settings));
        }

        public static void DumpKnownDlls(Action<IPrettyPrintable> Printer)
        {
            NtKnownDlls KnownDlls = new NtKnownDlls();
            Printer(KnownDlls);
        }

        public static void DumpApiSets(Action<IPrettyPrintable> Printer)
        {
            NtApiSet ApiSet = new NtApiSet();
            Printer(ApiSet);
        }

        public static void DumpApiSets(PE Application, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            NtApiSet ApiSet = new NtApiSet(Application);
            Printer(ApiSet);
        }

        public static void DumpManifest(PE Application, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            PEManifest Manifest = new PEManifest(Application);
            Printer(Manifest);
        }

        public static void DumpSxsEntries(PE Application, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            SxsDependencies SxsDeps = new SxsDependencies(Application);
            Printer(SxsDeps);
        }


        public static void DumpExports(PE Pe, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            PEExports Exports = new PEExports(Pe);
            Printer(Exports);
        }

        public static void DumpImports(PE Pe, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            PEImports Imports = new PEImports(Pe);
            Printer(Imports);
        }

        public static void DumpDependencyChain(PE Pe, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            PeDependencies Deps = new PeDependencies(Pe, recursion_depth);
            Printer(Deps);
        }

        public static void DumpModules(PE Pe, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false)
        {
            PeDependencies Deps = new PeDependencies(Pe, recursion_depth);

            if (!string.IsNullOrEmpty(filter))
            {

                var re = new Regex(filter);
                var me = new ModuleEntries();
                bool found = false;
                foreach (var keyValue in Deps.GetModules)
                {
                    if (re.IsMatch(keyValue.Key.Item1))
                    {
                        me.Add(keyValue.Key, keyValue.Value);
                        found = true;
                    }
                }
                if (found || !bare)
                {
                    if (bare)
                        Console.Write(Pe.Filepath);
                    Printer(me);
                }
            }
            else
                Printer(Deps.GetModules);
        }


        public static void DumpUsage(OptionSet opts)
        {
            string Usage = String.Join(Environment.NewLine,
                "Dependencies.exe : command line tool for dumping dependencies and various utilities.",
                "",
                "Usage : Dependencies.exe [OPTIONS] <FILE>",
                "",
                "Options :");
            Console.WriteLine(Usage);
            opts.WriteOptionDescriptions(Console.Out);

        }

        static Action<IPrettyPrintable> GetObjectPrinter(bool export_as_json, bool export_bare)
        {
            if (export_as_json)
                return JsonPrinter;

            var printer = new PrettyPrinter(export_bare);
            return (IPrettyPrintable obj) => printer.Print(obj);
        }


        public delegate void DumpCommand(PE Application, Action<IPrettyPrintable> Printer, int recursion_depth = 0, string filter = null, bool bare = false);

        static void Main(string[] args)
        {
            // always the first call to make
            Phlib.InitializePhLib();



            // Load singleton for binary caching
            string ApplicationLocalAppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dependencies"
            );

            // Load singleton for binary caching
            BinaryCache.Instance = new BinaryCacheImpl(ApplicationLocalAppDataPath, 200);


            //BinaryCache.Instance = new BinaryNoCacheImpl();
            BinaryCache.Instance.Load();

            int recursion_depth = 0;
            bool early_exit = false;
            bool show_help = false;
            bool export_as_json = false;
            bool export_bare = false;
            string fileMask = null;

            DumpCommand command = null;
            string filter = null;


            OptionSet opts = new OptionSet() {
                { "h|help",  "show this message and exit", v => show_help = v != null },
                { "json",  "Export results in json format", v => export_as_json = v != null },
                { "b|bare",  "Export results in bare format", v => export_bare = v != null },
                { "d|depth=",  "limit recursion depth when analysing loaded modules or dependency chain. Default value is infinite", (int v) =>  recursion_depth = v },
                { "f|filter=",  "regular expression filter of interested modules", (string v) =>  filter = v },
                { "m|mask=", "file mask", (string v) => fileMask = v },
                { "knowndll", "List all known dlls", v => { DumpKnownDlls(GetObjectPrinter(export_as_json,false));  early_exit = true; } },
                { "apisets", "List apisets redirections", v => { DumpApiSets(GetObjectPrinter(export_as_json,false));  early_exit = true; } },
                { "apisetsdll", "List apisets redirections from apisetschema <FILE>", v => command = DumpApiSets },
                { "manifest", "show manifest information embedded in <FILE>", v => command = DumpManifest },
                { "sxsentries", "dump all of <FILE>'s sxs dependencies", v => command = DumpSxsEntries },
                { "imports", "dump <FILE> imports", v => command = DumpImports },
                { "exports", "dump <FILE> exports", v => command = DumpExports },
                { "chain", "dump <FILE> whole dependency chain", v => command = DumpDependencyChain },
                { "modules", "dump <FILE> resolved modules", v => command = DumpModules },
        };

            List<string> eps = opts.Parse(args);

            if (early_exit)
                return;

            if ((show_help) || (args.Length == 0) || (command == null))
            {
                DumpUsage(opts);
                return;
            }

            if (eps.Count == 0)
            {
                Console.Error.WriteLine("[x] Command {0:s} needs to have a PE <FILE> argument", command.Method.Name);
                Console.Error.WriteLine("");

                DumpUsage(opts);
                return;
            }

            String FileName = eps[0];
            if (!string.IsNullOrEmpty(fileMask))
            {
                var filemask = new Regex(fileMask);
                foreach (string file in Directory.EnumerateFiles(FileName, "*.*", SearchOption.AllDirectories))
                {
                    if (filemask.IsMatch(file))
                        DumpFile(recursion_depth, export_as_json, export_bare, command, filter, file);
                }
            }
            else
            {
                DumpFile(recursion_depth, export_as_json, export_bare, command, filter, FileName);
            }

        }

        private static void DumpFile(int recursion_depth, bool export_as_json, bool export_bare, DumpCommand command, string filter, string FileName)
        {
            try
            {
                if (!NativeFile.Exists(FileName))
                {
                    Console.Error.WriteLine("[x] Could not find file {0:s} on disk", FileName);
                    return;
                }
                Debug.WriteLine("[-] Loading file {0:s} ", FileName);
                PE Pe = new PE(FileName);
                if (!Pe.Load())
                {
                    Console.Error.WriteLine("[x] Could not load file {0:s} as a PE", FileName);
                    return;
                }
                command(Pe, GetObjectPrinter(export_as_json, export_bare), recursion_depth, filter, export_bare);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while procesing: " + FileName + " " + e.ToString());
            }
        }
    }
}
