namespace Leo

open Rhino
open Rhino.Commands
open Rhino.Input
open Rhino.PlugIns


//TODO if you haven't run the command in a rhino session, history fails to update
//children.  After you run the command once, history works on objs created from 
//an earlier session

    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
[<AllowNullLiteral>]
[<System.Runtime.InteropServices.Guid(Leo.IDs.LeoPlugin)>]
type LeoPlugin() as this =  // don't set constructor private  for singelton  
    inherit Rhino.PlugIns.PlugIn()
    static let mutable instance = null

    do  
        instance <- this


    /////<summary>Gets the only instance of the MyRhinoPlugin1Plugin plug-in.</summary>
    //public static MyRhinoPlugin1Plugin Instance { get; private set; }
    static member val Instance = instance   


    //// You can override methods here to change the plug-in behavior on
    // loading and shut down, add options pages to the Rhino _Option command
    // and maintain plug-in wide options in a document.
