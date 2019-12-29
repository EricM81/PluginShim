namespace Shimmy
{

    #region Don't touch, you only need one

    public class Plugin : Rhino.PlugIns.PlugIn

    {
        public Plugin()
        {
            Instance = this;
        }


        public static Plugin Instance
        {
            get; private set;
        }

    
    }
    #endregion


}