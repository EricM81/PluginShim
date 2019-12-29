using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace PluginShim
{
    /// <summary>
    /// Find "PluginCommand" and replace all instances in the current document 
    /// with your plugin's class name 
    /// 
    /// Create one of these for each class that inherits from Rhino.Commands.Command 
    /// 
    /// </summary>
    public class ShimPluginCommand : Command
    {
        /// <summary>
        /// Set pluginNamespace to the namespace of your plugin's class name.
        /// Set pluginEnglishName to your English name.  'Shim' will be added as a 
        /// prefix to avoid a name collision
        /// 
        /// </summary>
        private static string pluginNamespace = "Plugin";
        private static string pluginEnglishName = "HelloWorld";

        #region Don't Touch 
        private static string pluginClassName = "PluginCommand";


        public ShimPluginCommand()
        {
            Instance = this;
        }
       
        public static ShimPluginCommand Instance
        {
            get; private set;
        }
        
        public override string EnglishName
        {
            get { return "Shim" + pluginEnglishName; }
        }
        #endregion

        /// <summary>
        /// If you are using this to debug more than one plugin assembly, 
        /// make sure you are using the correct ShimPlugin class.
        /// 
        /// </summary>
        
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return ShimPlugin.Execute(pluginNamespace + "." + pluginClassName, doc, mode);
        }

    

    }
}
