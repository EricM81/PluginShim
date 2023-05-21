using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Linq;


namespace SampleCsPlugin
{
    class MirrorXYZDisplay : DisplayConduit
    {
        
        public GeometryBase[] Geometry { get; set; }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            foreach (var geo in Geometry)
                e.IncludeBoundingBox(geo.GetBoundingBox(false));
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            base.DrawForeground(e);
            foreach (var geo in Geometry)
            {
                var color = System.Drawing.Color.Red;
                const int wirePixelsWidth = 2;
                const float pointCloudPtSize = 0.1f;
                switch (geo)
                {
                    case AnnotationBase annotation :
                        e.Display.DrawAnnotation(annotation, color); break;
                    case Brep brep:
                        e.Display.DrawBrepWires(brep, color, wirePixelsWidth); break;
                    case Curve curve:
                        e.Display.DrawCurve(curve, color); break;   
                    case Extrusion extrusion:
                        e.Display.DrawExtrusionWires(extrusion, color); break;  
                    case Mesh mesh: 
                        e.Display.DrawMeshWires(mesh, color, wirePixelsWidth); break; 
                    case Point point:   
                        e.Display.DrawPoint(point.Location, color); break;  
                    case PointCloud pointCloud:
                        e.Display.DrawPointCloud(pointCloud, pointCloudPtSize, color); break;
                    case SubD subD:
                        e.Display.DrawSubDWires(subD, color, wirePixelsWidth); break; 
                    case Surface surface:
                        e.Display.DrawSurface(surface, color, wirePixelsWidth); break;
                    case TextDot textDot: //no draw method for text dot (string * point3d)
                        e.Display.DrawPoint(textDot.Point, color); break;   
                }
            }
        }
    }

    public class MirrorXYZ : Command
    {
        public static Command Instance { get; private set; }

        public MirrorXYZ()
        {
            Instance = this;
        }

        private void SetShimInstance(Command shim)
        {
            Instance = shim;
        }

        public override string EnglishName => "MirrorXYZ";

        private OptionToggle _mirrorYZ = new OptionToggle(true, "No", "Yes");
        private OptionToggle _mirrorZX = new OptionToggle(false, "No", "Yes");
        private OptionToggle _mirrorXY = new OptionToggle(false, "No", "Yes");
        
        private int pown(int x, int exp)
        {
            return (int)Math.Pow(x, exp);
        }

        private Transform[] CreateXForms(bool yz, bool zx, bool xy)
        {
            var boolCount = Convert.ToInt32(yz) + Convert.ToInt32(zx) + Convert.ToInt32(xy);
            var xforms = new Transform[boolCount];
            var i = 0;

            if (yz)
            {
                xforms[i] = Transform.Mirror(Plane.WorldYZ);
                i++;
            }

            if (zx)
            {
                xforms[i] = Transform.Mirror(Plane.WorldZX);
                i++;
            }

            if (xy)
                xforms[i] = Transform.Mirror(Plane.WorldXY);

            var count = 1; // identity transform of the original object
            for (i = 0; i < boolCount; i++)
                count = count + pown(2, i);

            var retVal = new Transform[count];
            retVal[0] = Transform.Identity;
            for (var x = 0; x < xforms.Length; x++)
            {
                for (var y = pown(2, x); y < pown(2, (x + 1)); y++)
                    retVal[y] = Transform.Multiply(xforms[x], retVal[y - pown(2, x)]);
            }
            //remove the original transform identity
            return retVal.Skip(1).ToArray();
        }

        /// <summary>
        /// Creates one duplicate for each transform.  Return length == xforms.Length.
        /// </summary>
        /// <param name="geo">Obj to duplicate</param>
        /// <param name="xforms">Transforms to apply</param>
        /// <returns></returns>
        private GeometryBase[] DuplicateOnTransform(GeometryBase geo, Transform[] xforms)
        {
            var retVal = new GeometryBase[xforms.Length];

            for (var i = 0; i < retVal.Length; i++)
            {
                var dup = geo.Duplicate();
                dup.Transform(xforms[i]);
                retVal[i] = dup;
            }
            return retVal;
        }

        private void BuildOptions(GetBaseClass gb)
        {
            gb.AddOptionToggle("MirrorYZ", ref _mirrorYZ);
            gb.AddOptionToggle("MirrorZX", ref _mirrorZX);
            gb.AddOptionToggle("MirrorXY", ref _mirrorXY);
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            ObjRef[] objRefs;
            var display = new MirrorXYZDisplay();

            var getGeometry = new GetObject();
            getGeometry.EnablePreSelect(true, true);
            getGeometry.SubObjectSelect = false;
            getGeometry.SetCommandPrompt("Select object to mirror.");
            getGeometry.AcceptNothing(false);
            BuildOptions(getGeometry);

            while (true)
            {
                var res = getGeometry.GetMultiple(1, 0);
                if (getGeometry.CommandResult() != Result.Success)
                    return getGeometry.CommandResult();
                if (res != GetResult.Object)
                    continue;
                objRefs = getGeometry.Objects();

                break;
            }

            if (objRefs.Length == 0) return Result.Cancel;

            var previewOptions = new GetOption();
            previewOptions.AcceptNothing(true);
            previewOptions.AcceptEnterWhenDone(true);
            previewOptions.SetCommandPrompt("Press Enter to accept.");
            BuildOptions(previewOptions);

            while (true)
            {
                var xforms = CreateXForms(_mirrorYZ.CurrentValue, _mirrorZX.CurrentValue, _mirrorXY.CurrentValue);
                var geos = new GeometryBase[xforms.Length * objRefs.Length];
                for (int i = 0; i < objRefs.Length; i++)
                {
                    var children = DuplicateOnTransform(objRefs[i].Geometry(), xforms);
                    Array.Copy(children, 0, geos, i * xforms.Length, xforms.Length);

                }
                display.Geometry = geos;
                display.Enabled = true;
                doc.Views.Redraw();

                var ret = previewOptions.Get();

                display.Enabled = false;
                var res = previewOptions.CommandResult();

                if (res != Result.Success) return previewOptions.CommandResult();

                if (ret == GetResult.Nothing)
                {
                    for (var x = 0; x < objRefs.Length; x++)
                    {
                        for (var y = x * xforms.Length; y < x * xforms.Length + xforms.Length; y++)
                        {
                            var h = WriteHistory(objRefs[x]);
                            doc.Objects.Add(geos[y], objRefs[x].Object().Attributes, h, false);
                        }
                    }
                    break;
                }

            }

            return Result.Success;  
        }

        private HistoryRecord WriteHistory(ObjRef objRef)
        {
            var history = new HistoryRecord(Instance, 1);
            if (!history.SetObjRef(0, objRef))
                return null;
            if (!history.SetBool(1, _mirrorYZ.CurrentValue))
                return null;
            if (!history.SetBool(2, _mirrorZX.CurrentValue))
                return null;
            if (!history.SetBool(3, _mirrorXY.CurrentValue))
                return null;

            return history;
        }

        protected override bool ReplayHistory(ReplayHistoryData replayData)
        {
            var objRef = replayData.GetRhinoObjRef(0);
            if (objRef == null) return false;
            if (!replayData.TryGetBool(1, out var yz)) return false;
            if (!replayData.TryGetBool(1, out var zx)) return false;
            if (!replayData.TryGetBool(1, out var xy)) return false;

            var xforms = CreateXForms(yz, zx, xy);
            var geos = DuplicateOnTransform(objRef.Geometry(), xforms);
            if (geos.Length != replayData.Results.Length) return false;
            for (var i = 0; i < replayData.Results.Length; i++)
            {   
                var item = replayData.Results[i];
                var att = item.ExistingObject.Attributes;

                switch (geos[i])
                {
                    case AngularDimension a:
                        item.UpdateToAngularDimension(a, att); break;  
                    case Curve a:
                        item.UpdateToCurve(a, att); break;  
                    case Brep a:
                        item.UpdateToBrep(a, att); break;  
                    case Surface a:
                        item.UpdateToSurface(a, att); break;
                    case Hatch a:
                        item.UpdateToHatch(a, att); break;
                    case Leader a:
                        item.UpdateToLeader(a, att); break;
                    case LinearDimension a:
                        item.UpdateToLinearDimension(a, att); break;
                    case Mesh a:
                        item.UpdateToMesh(a, att); break;  
                    case Point a:
                        item.UpdateToPoint(a.Location, att); break;
                    case PointCloud a:
                        item.UpdateToPointCloud(a, att); break;
                    case RadialDimension a:
                        item.UpdateToRadialDimension(a, att); break;
                    case SubD a:
                        item.UpdateToSubD(a, att); break;
                    case TextDot a:
                        item.UpdateToTextDot(a, att); break;
                    default:
                        RhinoApp.WriteLine($"RhinoCommon history does not support {geos[i].GetType().Name}."); return false;
                }
            }
            return true;
            
        }
    }
}