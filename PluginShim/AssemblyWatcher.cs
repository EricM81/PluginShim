using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Shimmy
{
    internal class TargetType

    {   // TODO Set ShimDirectoryName
        /// <summary>
        /// You need to make two copies of your target plugin's build output.  One copy is for Rhino to consume, which will hold a lock
        /// on the file until you exit the application.  The other copy is for this shim to consume.
        /// The shim's copy should be somewhere in it's root folder structure.  PluginShim will search each parent path for a folder
        /// matching the name you supply to ShimDirectoryName, and try to load the assemblies from there.
        ///
        /// If you have a problem loading dependencies, try using fuslogvw to see the folder locations that .NET is searching.
        /// https://learn.microsoft.com/en-us/dotnet/framework/tools/fuslogvw-exe-assembly-binding-log-viewerusing 
        /// </summary>
        private const string ShimDirectoryName = "_ShimCopy";

        public readonly string RhpFullPath;
        public readonly string PdbFullPath;
        public readonly string CommandName;
        public readonly string CommandFullName;
        public readonly Command ShimInstanceObj;

        public TargetType(string rhpName, string pdbName, string commandNameSpace, string commandName, Command shimInstanceObj)
        {
            CommandName = commandName;
            CommandFullName = $"{commandNameSpace}.{commandName}";
            ShimInstanceObj = shimInstanceObj;

            var shimFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
            if (shimFolder == null || !shimFolder.Exists)
                return;

            var curDirectory = shimFolder;

            while (true)
            {
                if (curDirectory == null || !curDirectory.Exists || curDirectory.FullName == curDirectory.Root.FullName)
                {
                    throw new Exception($"Could not find {ShimDirectoryName} in parent directory structure. \n{shimFolder.FullName}");
                }

                foreach (var dir in curDirectory.GetDirectories())
                {
                    if (dir.Name != ShimDirectoryName) continue;
                    RhpFullPath = Path.Combine(dir.FullName, rhpName);
                    PdbFullPath = Path.Combine(dir.FullName, pdbName);
                    return;
                }
                curDirectory = curDirectory.Parent;
            }
        }
    }

    internal class TargetInstance
    {
        public TargetType TargetType;
        private Command CommandObj { get; set; }
        private MethodInfo RunMethod { get; set; }
        private MethodInfo ReplayMethod { get; set; }

        public TargetInstance(Assembly asm, TargetType targetType)
        {
            TargetType = targetType;
            var obj = asm.CreateInstance(targetType.CommandFullName);
            if (!(obj is Command cmd)) throw new Exception($"Could not load {targetType.CommandFullName}::Command via reflection.");

            CommandObj = cmd;
            RunMethod = cmd.GetType().GetMethod("RunCommand", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (RunMethod == null) throw new Exception($"Could not find RunCommand method for {targetType.CommandName}");

            ReplayMethod = cmd.GetType().GetMethod("ReplayHistory", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ReplayMethod == null) throw new Exception($"Could not find ReplayMethod method for {targetType.CommandName}");

            var mSetInstance = cmd.GetType().GetMethod("SetShimInstance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mSetInstance == null)
                throw new Exception($"{targetType.CommandName} does not support SetShimInstance for history replay");
            else
                mSetInstance.Invoke(cmd, new object[] { targetType.ShimInstanceObj });
        }

        public Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var returnValue = RunMethod.Invoke(CommandObj, new object[] { doc, mode });
            if (!(returnValue is Result result))
                throw new Exception("RunCommand did not return a value of type Rhino.Commands.Result.");
            return result;
        }

        public bool ReplayHistory(ReplayHistoryData replayData)
        {
            var returnValue = ReplayMethod.Invoke(CommandObj, new object[] { replayData });
            if (!(returnValue is bool result))
                throw new Exception("ReplayHistory did not return a value of type Boolean.");
            return result;
        }
    }

    internal class WatchedAssembly
    {
        public FileSystemWatcher Watcher { get; }
        public Assembly Assembly { get; set; }
        public List<TargetInstance> Targets { get; set; }

        public WatchedAssembly(FileSystemWatcher watcher, Assembly assembly)
        {
            Watcher = watcher;
            Assembly = assembly;
            Targets = new List<TargetInstance>();
        }
    }

    internal class AssemblyWatcher
    {
        private static readonly Dictionary<string, WatchedAssembly> _dict = new Dictionary<string, WatchedAssembly>();

        private static FileSystemWatcher RegisterNewWatcher(string pathToDll, string pathToPdb)
        {
            var directory = Path.GetDirectoryName(pathToDll);
            if (string.IsNullOrEmpty(directory))
                return null;

            var watcher = new FileSystemWatcher(directory)
            {
                Filter = Path.GetFileName(pathToDll),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };

            watcher.Changed += (sender, e) =>
            {
                // TODO: Set timeout
                // Depending on your setup, the file watcher might grab your plugin or debug symbols before they finish
                // writing to the file system.  The file watcher is triggered on a change to the dll, and it was observed
                // that the old pdb was being loaded.  Weird msbuild things.
                // 4 seconds was a comfortable margin for me.  It always took about that long to go from watching the build
                // output complete to swapping back to Rhino to setting up my next test. 
                System.Threading.Thread.Sleep(4000);

                if (!_dict.TryGetValue(pathToDll, out var wa)) return;

                // Assembly was reloaded.  Release old instances
                wa.Targets = new List<TargetInstance>();
                //Load new Assembly.  Future calls will build new target instances.
                wa.Assembly = Assembly.Load(File.ReadAllBytes(pathToDll), File.ReadAllBytes(pathToPdb));
            };
            return watcher;
        }

        private static WatchedAssembly GetWatchedAssembly(string pathToDll, string pathToPdb)
        {
            if (_dict.TryGetValue(pathToDll, out var wa))
                return wa;
            else
            {
                var asm = Assembly.Load(File.ReadAllBytes(pathToDll), File.ReadAllBytes(pathToPdb));
                var fsw = RegisterNewWatcher(pathToDll, pathToPdb);
                wa = new WatchedAssembly(fsw, asm);
                _dict.Add(pathToDll, wa);
                return wa;
            }
        }

        public static TargetInstance GetShimmedType(TargetType targetType)
        {
            var wa = GetWatchedAssembly(targetType.RhpFullPath, targetType.PdbFullPath);
            if (wa == null) return null;
            foreach (var shim in wa.Targets.Where(shim => shim.TargetType.CommandFullName == targetType.CommandFullName))
                return shim;

            // if the assembly does not have an instance of the target command, create one
            var newShim = new TargetInstance(wa.Assembly, targetType);
            wa.Targets.Add(newShim);
            return newShim;
        }
    }
}
