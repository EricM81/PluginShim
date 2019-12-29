namespace PluginShim
{

    #region Don't touch, you only need one

    public class PluginShim : Rhino.PlugIns.PlugIn

    {
        public PluginShim()
        {
            Instance = this;
        }


        public static PluginShim Instance
        {
            get; private set;
        }

    
    }
    #endregion


}