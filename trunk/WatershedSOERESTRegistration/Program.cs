using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS;
using ESRI.ArcGIS.ADF.Connection.AGS;

namespace WatershedSOERESTRegistration
{
    class Program
    {
        static void Main(string[] args)
        {
            AGSServerConnection agsServerConnection = new AGSServerConnection();
            agsServerConnection.Host = "192.171.192.6";
            agsServerConnection.Connect();
            IServerObjectAdmin2 serverObjectAdmin = (IServerObjectAdmin2)agsServerConnection.ServerObjectAdmin;
            // this name must match that defined for property pages
            string extensionName = "WatershedSOE";

            if (args.Length == 1 && args[0] == "/unregister")
            //if(true)
            {
                // check whether the soe is already registered
                if(ExtensionRegistered(serverObjectAdmin,extensionName))
                {
                    // delete the SOE
                    serverObjectAdmin.DeleteExtensionType("MapServer",extensionName);
                    Console.WriteLine(extensionName+" successfully unregistered");
                }
                else
                {
                    Console.WriteLine(extensionName + " is not registered with ArcGIS Server");
                }

            }
            else
            {
                if (!ExtensionRegistered(serverObjectAdmin, extensionName))
                {
                    IServerObjectExtensionType3 serverObjectExtensionType = (IServerObjectExtensionType3)serverObjectAdmin.CreateExtensionType();
                    // must match namespace and classname of the class implementing IServerObjectExtension
                    serverObjectExtensionType.CLSID = "WatershedSOE.WatershedSOE";
                    serverObjectExtensionType.Description = "Creates watershed above an input location";
                    serverObjectExtensionType.Name = extensionName;
                    serverObjectExtensionType.DisplayName = "Watershed REST";
                    serverObjectExtensionType.Properties.SetProperty("FlowAccum", "fac");
                    serverObjectExtensionType.Properties.SetProperty("FlowDir", "fdr");
                    serverObjectExtensionType.Info.SetProperty("SupportsREST", "true");
                    serverObjectExtensionType.Info.SetProperty("SupportsMSD", "true");
                    serverObjectAdmin.AddExtensionType("MapServer", serverObjectExtensionType);
                    Console.WriteLine(extensionName + " succesfully registered with ArcGIS Server");
                }
                else
                {
                    Console.WriteLine(extensionName + " is already registered with ArcGIS Server");
                }
            }
            Console.ReadLine();
        }
        static private bool ExtensionRegistered(IServerObjectAdmin2 serverObjectAdmin, string extensionName)
        {
            IEnumServerObjectExtensionType extensionTypes = serverObjectAdmin.GetExtensionTypes("MapServer");
            extensionTypes.Reset();
            IServerObjectExtensionType extensionType = extensionTypes.Next();
            while (extensionType != null)
            {
                if (extensionType.Name == extensionName)
                {
                    return true;
                }
                extensionType = extensionTypes.Next();
            }
            return false;
        }
    }
}
