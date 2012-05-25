using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SOEUtilities
{
    [System.Runtime.InteropServices.GuidAttribute(
        "5514323A-02F8-48d3-B7BA-5BF07AD36F49")]
    public abstract class SOEPropertyPage: ESRI.ArcGIS.Framework.IComPropertyPage,
        ESRI.ArcGIS.CatalogUI.IAGSSOEParameterPage
    {
        [System.Runtime.InteropServices.ComRegisterFunction()]
        static void RegisterFunction
            (String regKey)
        {
            Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(regKey.Substring(18) +
                "\\Implemented Categories\\" + "{A585A585-B58B-4560-80E3-87A411859379}");
        }
        [System.Runtime.InteropServices.ComUnregisterFunction()]
        static void UnregisterFunction(String regKey)
        {
            Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(regKey.Substring(18));
        }
        // abstract members define things that subclasses must implement
        public abstract ESRI.ArcGIS.Framework.IComPropertyPageSite PageSite
        {
            set;
        }
        public abstract int Activate();
        public abstract void Show();
        public abstract void Hide();
        // virtual members define things that subclasses can but need not implement
        public virtual int Height
        {
            get
            {
                return 0;
            }
        }
        public virtual void Deactivate() { }
        // remaining members of IComPropertyPage are not used by implementations
        // of SOE property pages. So implement them with do-nothing things so that
        // subclasses cannot implement
        public bool IsPageDirty
        {
            get
            {
                return false;
            }
        }
        public string Title
        {
            get
            {
                return null;
            }
            set { }
        }
        public int Priority { get { return 0; } set { } }
        public string HelpFile { get { return null; } }
        public int Width { get { return 0; } }
        public void Apply() { }
        public void Cancel() { }
        public int get_HelpContextID(int controlID) { return 0; }
        public void SetObjects(ESRI.ArcGIS.esriSystem.ISet objects) { }
        public bool Applies(ESRI.ArcGIS.esriSystem.ISet objects)
        {
            return false;
        }
        // Implement as abstract members to do with setting and getting the 
        // configured properties from the config file
        public abstract ESRI.ArcGIS.esriSystem.IPropertySet ServerObjectProperties
        {
            get;
            set;
        }
        public abstract ESRI.ArcGIS.esriSystem.IPropertySet ExtensionProperties
        {
            get;
            set;
        }
        public abstract string ServerObjectExtensionType
        {
            get;
        }
        public abstract string ServerObjectType
        {
            get;
        }
    }
}
