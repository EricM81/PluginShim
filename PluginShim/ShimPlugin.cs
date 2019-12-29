using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Rhino;
using Rhino.Commands;
using System.Reflection;


namespace Shimmy
{
    /// <summary>
    /// You can use this to shim multiple plugins.
    /// Just create a new static class for each plugin assembly
    /// </summary>
    public class ShimPlugin
    {

        /// <summary>
        /// Set file location to the bin\Debug and obj\Debug folder for the rhp/pdb files
        /// </summary>
        static ShimPlugin()
        {
            pathToBinRhp = @"C:\Users\EricM\Documents\Dev\PluginShim\Plugin\bin\Plugin.rhp";
            pathToBinPdb = @"C:\Users\EricM\Documents\Dev\PluginShim\Plugin\bin\Plugin.pdb";
            pathToObjRhp = @"C:\Users\EricM\Documents\Dev\PluginShim\Plugin\obj\Debug\Plugin.rhp";
            pathToObjPdb = @"C:\Users\EricM\Documents\Dev\PluginShim\Plugin\obj\Debug\Plugin.pdb";
        }


        private static string pathToBinRhp;
        private static string pathToBinPdb;
        private static string pathToObjRhp;
        private static string pathToObjPdb;

        /// <summary>
        /// Don't change anything here.
        /// 
        /// This uses reflection to dynamically load the plugin assembly, which allows
        /// you to debug a new build without having to restart the debugger.
        /// 
        /// </summary>
        /// <param name="commandClassName"> Name of the class that inherits from Rhino.Commands.Command class</param>
        /// <param name="doc"> parameter pass through to RunCommand() </param>
        /// <param name="mode"> parameter pass through to RunCommand() </param>
        /// <returns> Rhino.Result </returns>

        public static Result Execute(string commandClassName, RhinoDoc doc, RunMode mode)
        {
            if (File.Exists(pathToObjRhp)) { File.Delete(pathToObjRhp); }
            if (File.Exists(pathToObjPdb)) { File.Delete(pathToObjPdb); }

            Assembly asm = Assembly.Load(File.ReadAllBytes(pathToBinRhp), File.ReadAllBytes(pathToBinPdb));
            Object obj = asm.CreateInstance(commandClassName);
            if (obj != null)
            {
                MethodInfo m = obj.GetType().GetMethod("RunCommand", BindingFlags.Instance | BindingFlags.NonPublic);
                
                //Set your breakpoint here
                return (Result)m.Invoke(obj, new Object[] { doc, mode });
            }
            else
                return Result.Failure;
        }

    }
}
