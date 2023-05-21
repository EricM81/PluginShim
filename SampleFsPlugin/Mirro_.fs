namespace Leo.Open

open System
open Microsoft.FSharp.Core
open Rhino.Commands
open Rhino.DocObjects
open Rhino.Geometry
open Rhino.Input
open Rhino.Input.Custom
open Leo.Types
open Leo.PluginTypes
open Rhino
open Rhino.Display

//open Leo.PluginType

// This command will shadow Rhino's native Mirror command.  Alpha sorted, Mirro will autocomplete before Mirror.
// Rhino uses a fuzzy sort for commands, which includes a fuzzy search along with priority based on usage.
// If a user chooses Mirror over Mirro, Rhino will autocomplete Mirr -> Mirror going forward.
// module UnSafe =
//     
//     [<DllImport("rhcommon_c", CallingConvention = CallingConvention.Cdecl)>]
//     

module Mirro_Param =
    [<Struct>]
    type Options =
        {
            UnP: bool
            VnP: bool
            UvP: bool
        }
    [<Struct>]
    type Input =
        {
            Surface: ObjWrapper<Brep>
            Parents: ObjRef[]
            Options: Options
        }
    [<Struct>]
    type History =
        {
            Surface: ObjWrapper<Brep>
            Parent: ObjRef
            Options: Options

        }
    let createOptions unP vnP uvP =
        { UnP = unP; VnP = vnP; UvP = uvP }
    let createInput opt srf =
        {Input.Surface = srf; Parents = Unchecked.defaultof<_>; Options = opt} 
        
    let replaceParents (param: Input) (parents: ObjRef[]) =
        { param with Parents =  parents }
        
    let createHistory srf geo opt =
        {History.Surface = srf; Parent = geo; Options = opt }
    let inputsToHistory (a: Input) =
        let mapper (p: ObjRef) =
            {Surface =  a.Surface; Parent = p; Options = a.Options }
        a.Parents |> Array.map mapper
    
        
    let createMirrorXForms (srf: ObjWrapper<Brep>) (opts: Options) =
        let count = Convert.ToInt32(opts.UnP) + Convert.ToInt32(opts.VnP) + Convert.ToInt32(opts.UvP)
        let xforms: Transform[] = Array.zeroCreate count
        let mutable i = 0
        
        let origin = SrfToPlaneParam.createCenter srf.Obj.Surfaces[0] |> SrfToPlaneParam.calcFrame
        if opts.UnP then
            xforms[i] <- origin.Clone() |> SrfToPlaneParam.flipPlaneNtoU  |> Transform.Mirror
            i <- i + 1
        if opts.VnP  then
            xforms[i]  <- origin.Clone() |> SrfToPlaneParam.flipPlaneNtoV |> Transform.Mirror
            i <- i + 1
        if opts.UvP then
            xforms[i]  <- Transform.Mirror origin

        xforms
        
    let transformParent (a: History) =
        let xforms = createMirrorXForms a.Surface a.Options
        let count = int(Math.Pow(2, float(xforms.Length)))
        let arr: GeometryBase[] = Array.zeroCreate count
        arr[0] <- a.Parent.Geometry()
        
        for x = 0 to xforms.Length - 1 do
            for i = (pown 2 (x)) to (pown 2 (x + 1)) - 1 do
                let clone = arr[i - (pown 2 (x))].Duplicate()
                let b = clone.Transform(xforms[x])
                arr[i] <- clone
        arr[1..] 
                
    let objFilter =
        ObjectType.Annotation |||
        ObjectType.Brep |||
        ObjectType.Curve |||
        ObjectType.Extrusion |||
        ObjectType.Mesh |||
        ObjectType.Point |||
        ObjectType.SubD |||
        ObjectType.Surface |||
        ObjectType.TextDot |||
        ObjectType.InstanceReference
        
type Mirro_Display ()  = 
    inherit Display.DisplayConduit()
    let mutable mirrors: Surface[] = Unchecked.defaultof<_>

    member this.SetObjects (m: Mirro_Param.Input) = 
        let currSrf = m.Surface.Obj.Surfaces[0]
        let mapper (x:Transform) =
            let newSrf = currSrf.Duplicate() :?> Surface
            let b = newSrf.Transform(x)
            newSrf
            
        mirrors <- Mirro_Param.createMirrorXForms m.Surface m.Options |> Array.map mapper

    override this.CalculateBoundingBox (e: CalculateBoundingBoxEventArgs) = 
        base.CalculateBoundingBox(e)
        let iter (x: GeometryBase) = 
            e.IncludeBoundingBox(x.GetBoundingBox(false))
        mirrors |> Array.iter iter

    override this.DrawForeground(e: DrawEventArgs) =
        base.DrawForeground(e)
        let iterMirrs (s: Surface) = 
            e.Display.DrawMeshShaded(Mesh.CreateFromSurface(s, new MeshingParameters(0)), new DisplayMaterial(Drawing.Color.Red,0.5))
        mirrors |> Array.iter iterMirrs

[<AllowNullLiteral>]
[<System.Runtime.InteropServices.Guid(Leo.IDs.LeoMirro_)>]
type Mirro_ () as this = 
    inherit Command()
    let HISTORY_VERSION = 20230508

    static let mutable instance: Mirro_ = null
    
    //if you don't want settings to persist between calls, use function level mutable variables
    //if you want settings to persist between commands, use mutable class level variables.
    //If you want them to persist for this user profile between Rhino sessions,
    //use this.Settings (type of PersistentSettings).
    let mutable unP: OptionToggle = new OptionToggle(true, "Off", "On")
    let mutable vnP: OptionToggle = new OptionToggle(false, "Off", "On")
    let mutable uvP: OptionToggle = new OptionToggle(false, "Off", "On")
    
    do 
        instance <- this
    
        
    let WriteHistory (x: Mirro_Param.History) =
        //I think I need to make a computation expression to chain these together
        
        //let (>>=) b f =
        //    match b with
        //    | true -> f
        //    | false -> false

        let history = new HistoryRecord(this, HISTORY_VERSION)

        //for debugging 
        let returnBools: bool[] = [|
            history.SetObjRef(0, x.Surface.ObjRef)
            history.SetObjRef(1, x.Parent) 
            history.SetBool(2, x.Options.UnP)
            history.SetBool(3, x.Options.VnP)
            history.SetBool(4, x.Options.UvP)
        |]
        // TODO Smart Assembly doesn't support text formatter with pruning
        //sprintf "%A" returnBools |> RhinoApp.WriteLine
        
        
        history

    let ReadHistory (replay: ReplayHistoryData): Result<Mirro_Param.History, unit> =
        let historyVersion = replay.HistoryVersion
        // TODO make sure history version are compatible
        
        let srfP, srfOW = ObjWrapper.tryGet<Brep>(replay.GetRhinoObjRef(0))
        let geo = replay.GetRhinoObjRef(1)
        let geoP = not (Object.ReferenceEquals(geo, null))
        let unPP, unP = replay.TryGetBool(2)
        let vnPP, vnP = replay.TryGetBool(3)
        let uvPP, uvP = replay.TryGetBool(4)
        if srfP && geoP && unPP && vnPP && uvPP then
            Mirro_Param.createOptions unP vnP uvP |> Mirro_Param.createHistory srfOW geo |> Ok
        else Error ()
           
    ///RhinoDoc * RunMode -> Result
    /// RunMode tells if it is scripted (hyphen prefix) or interactively
    override this.RunCommand (doc, _): Result  =
        
        let displayConduit = new Mirro_Display()

        let addOptions (gb: GetBaseClass) =
            gb.AddOptionToggle("UN", &unP) |> ignore
            gb.AddOptionToggle("VN", &vnP) |> ignore
            gb.AddOptionToggle("UV", &uvP) |> ignore

        let getOptions () =
            Mirro_Param.createOptions unP.CurrentValue vnP.CurrentValue uvP.CurrentValue
        
        let processOptions () =
            let opt = getOptions ()
            if opt.UnP || opt.VnP || opt.VnP then ()
            else
                RhinoApp.WriteLine("At least one plane must be selected")
                unP.CurrentValue <- true
                
        let updateOptions (param: Mirro_Param.Input)=
            //open crvs are only valid with a single mirror
            //with two or more, the curve must be closed
            processOptions()
            {param with Options = (getOptions ())}

        let updateDisplayConduit (param: Mirro_Param.Input) = 
            displayConduit.SetObjects param
            displayConduit.Enabled <- true
            doc.Views.Redraw ()
            param
                
        let turnOffDisplayConduit () = 
            displayConduit.Enabled <- false
            doc.Views.Redraw ()

            
        let rec getSrf (): Result<Mirro_Param.Input, Result> =
            let go = new GetObject()
            go.GeometryFilter <- ObjectType.Surface
            go.EnablePreSelect(false, true)
            go.SubObjectSelect <- false
            go.SetCommandPrompt("Pick a base surface for mirroring")
            addOptions go
          
            match go.Get(), go.CommandResult(), go.ObjectCount with
            | GetResult.Object, Result.Success, 1 ->  //surface was selected, proceed to next step
                match go.Object(0).Geometry() with
                | :? Brep as s when s.IsSurface = true ->
                    //breps can be a single surface (IsSurface) or multiple surfaces joined together (Faces)
                    ObjWrapper<Brep>(go.Object(0), s)
                    |> Mirro_Param.createInput (getOptions ()) 
                    |> updateDisplayConduit
                    |> Ok 
                | _ ->
                    //I don't think this is reachable.
                    getSrf()
            | GetResult.Cancel, _, _
            | GetResult.Timeout, _, _ 
            | _, Result.Cancel, _ -> //all of our abort cases I think?
                Error Result.Cancel
            | _, _, _ ->  //miss click, interacting with command line options, etc; go again
                processOptions () 
                getSrf ()
            
        let rec getGeo (param: Mirro_Param.Input): Result<Mirro_Param.Input, Result> =
                    
            let go = new GetObject()
            go.EnablePreSelect(false, true)
            go.GeometryFilter <- Mirro_Param.objFilter
            //go.AcceptNothing true
            //go.AcceptEnterWhenDone true
            go.SetCommandPrompt("Pick objects to mirror")
            addOptions go
            

            match go.GetMultiple(1,0), go.CommandResult() with
            | GetResult.Object, Result.Success -> //geometry was selected
                go.Objects() 
                |> Mirro_Param.replaceParents param
                |> updateOptions
                |> updateDisplayConduit
                |> Ok
            | GetResult.Cancel, _
            | GetResult.Timeout, _
            | _, Result.Cancel -> //all of our abort cases I think?
                Error Result.Cancel
            //| _, _ when go.GotDefault() && param.Curves.Length > 0 -> //user pressed Enter (or equivalent) when nothing changed
            //    Ok param
            | _, _ ->  //miss click, interacting with command line options, etc; go again
                updateOptions param
                |> updateDisplayConduit
                |> getGeo 

        let rec showPreview (param: Mirro_Param.Input): Result<Mirro_Param.Input, Result> = 
            let go = new GetOption()
            go.AcceptNothing(true)
            go.AcceptEnterWhenDone(true)
            go.SetCommandPrompt("Confirm mirror settings")
            addOptions go
          
            let rs = go.Get()

            match rs, go.CommandResult() with
            | GetResult.Nothing, Result.Success -> Ok param
            | GetResult.Option, Result.Success ->  //surface was selected, proceed to next step
                updateOptions param |> updateDisplayConduit |> showPreview
            | GetResult.Cancel, _
            | GetResult.Timeout, _ 
            | _, Result.Cancel -> //all of our abort cases I think?
                Error Result.Cancel
            | _, _ ->  //miss click, interacting with command line options, etc; go again
                updateOptions param |> updateDisplayConduit |> showPreview
            
        let rec doCommand () =

            getSrf ()
            |> Result.bind getGeo
            |> Result.bind showPreview
            |> function
            | Ok inputParam ->
                let histParams = Mirro_Param.inputsToHistory inputParam
                let iterDoc (h: HistoryRecord) i (g: GeometryBase) =
                    match g with
                    | :? InstanceReferenceGeometry as a ->
                        let guid = a.ParentIdefId
                        let id = doc.InstanceDefinitions.FindId(guid)
                        //doc.Objects.AddInstanceObject(id.Index, a.Xform, null, h, id.IsReference) |> ignore
                        let guid = inputParam.Parents[i].ObjectId
                        let c = doc.Objects.FindId(guid)
                        let b = new ObjRef(doc, guid)
                        doc.Objects.TransformWithHistory(b, a.Xform) |> ignore
                    | _ -> 
                        doc.Objects.Add(g, null, h, false) |> ignore
                    
                let iterHist (param: Mirro_Param.History) =
                    let history = WriteHistory param
                    Mirro_Param.transformParent param |> Array.iteri (iterDoc history)
                
                histParams |> Array.iter iterHist 
                

                Result.Success
                
            | Error r -> 
                turnOffDisplayConduit ()
                r
        try
            try
                doCommand ()
            with 
            | ex -> 
                RhinoApp.WriteLine(ex.Message)
                RhinoApp.WriteLine(ex.InnerException.Message)
                Result.Failure
        finally 
            turnOffDisplayConduit ()

    override this.ReplayHistory (e: ReplayHistoryData) =
        
        let update (y: ReplayHistoryResult) (x: GeometryBase): bool =
            let attrib = y.ExistingObject.Attributes
            match x with
            | :? AngularDimension as a -> y.UpdateToAngularDimension(a, attrib)
            | :? Brep as a -> y.UpdateToBrep(a, attrib)
            | :? Curve as a -> y.UpdateToCurve (a, attrib)
            | :? Extrusion as a -> y.UpdateToExtrusion (a, attrib)
            | :? LinearDimension as a -> y.UpdateToLinearDimension (a, attrib)
            | :? Mesh as a -> y.UpdateToMesh (a, attrib)
            | :? Point as a -> y.UpdateToPoint (a.Location, attrib)
            | :? RadialDimension as a -> y.UpdateToRadialDimension (a, attrib)
            | :? SubD as a -> y.UpdateToSubD (a, attrib)
            | :? Surface as a -> y.UpdateToSurface(a, attrib)
            | :? TextEntity as a -> y.UpdateToText (a, attrib)
            | :? TextDot as a -> y.UpdateToTextDot (a, attrib)
            | _ -> false
        
            
        match ReadHistory e with
            | Ok histParam ->
                let geo = Mirro_Param.transformParent histParam
                if e.Results.Length = geo.Length then
                    let mutable i = -1
                    let rec loop (last: bool): bool =
                        i <- i + 1
                        if last && i < geo.Length then 
                            update e.Results[i] geo[i]
                            |> loop 
                        else
                            last
                    loop true
                else
                    false 
            | Error _ -> false
            
    static member Instance = instance
    override this.EnglishName = "Mirro"  


    