//using System;
//using System.Collections.Generic;
//using Rhino;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Input;
//using Rhino.Input.Custom;

//namespace Shimmy
//{
    
//    public class ShimCmd2 : Command
//    {
//        // TODO: Update these four strings with the names of your target plugin
//        /// <summary>
//        /// The AssemblyWatcher will look for these files in ShimDirectoryName.
//        /// 
//        /// The filename of your target plugin.  You do not have to rename the dll to rhp.
//        /// The filename of your target debugging symbols.
//        /// The parent namespace of your target Command.
//        /// The class name of your derived Command.
//        /// </summary>
//        private const string RhpName = "SampleCsPlugin.dll";
//        private const string PdbName = "SampleCsPlugin.pdb";
//        private const string TargetNamespace = "SampleCsPlugin";
//        private const string TargetCommandName = "MirrorXYZ";

//        private readonly TargetType _targetType;

//        public ShimCmd2()
//        {
//            Instance = this;
//            _targetType = new TargetType(RhpName, PdbName, TargetNamespace, TargetCommandName, this);
//        }
       
//        public static ShimCmd2 Instance { get; private set; }

//        public override string EnglishName => "Shimmy" + _targetType.CommandName;


//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            var cmd = AssemblyWatcher.GetShimmedType(_targetType);
//            return cmd.RunCommand(doc, mode);
//        }

//        protected override bool ReplayHistory(ReplayHistoryData replayData)
//        {
//            var cmd = AssemblyWatcher.GetShimmedType(_targetType);
//            return cmd.ReplayHistory(replayData);
//        }
//    }
//}
