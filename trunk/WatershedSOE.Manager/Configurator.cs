using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.Geodatabase;
// TODO needs to reference ESRI.ARcGIS.ServerManager but this requires web applications part of AGS
namespace WatershedSOE.Manager
{
    public class Configurator : ESRI.ArcGIS.ServerManager.IServerObjectExtensionConfigurator
    {
        private ServerLogger logger = new ServerLogger();
        
        private System.Web.UI.WebControls.DropDownList m_FlowAccDropDown = new
            System.Web.UI.WebControls.DropDownList();
        private System.Web.UI.WebControls.DropDownList m_FlowDirDropDown = new
            System.Web.UI.WebControls.DropDownList();
        private System.Web.UI.WebControls.CheckBoxList m_ExtractionLayersDropDown = new 
            System.Web.UI.WebControls.CheckBoxList();
        private System.Web.UI.WebControls.CheckBox m_AllowInputPolygon = new 
            System.Web.UI.WebControls.CheckBox();

        private string m_flowacc;
        private string m_flowdir;
       // private string m_extractionlayers;// needs to be array
       // private bool m_inputPolyAllowed;
       // private string m_jsonServiceLayers = "{}";

        public string LoadConfigurator(ESRI.ArcGIS.Server.IServerContext serverContext,
            System.Collections.Specialized.NameValueCollection ServerObjectProperties, 
            System.Collections.Specialized.NameValueCollection ExtensionProperties, 
            System.Collections.Specialized.NameValueCollection InfoProperties, 
            bool isEnabled, 
            string servicesEndPoint, 
            string serviceName, 
            string serviceTypeName)
        {
            logger.LogMessage(ServerLogger.msgType.warning, "SOE manager page", 8000,
                         "SOE Manager page: Loading");
        
            // Just return a message if the SOE is not enabled on the current service.
            if (!isEnabled)
                return ("<span>No Properties to configure, sorry</span>");
            // Initialize member variables holding the SOE's properties.
            if (!string.IsNullOrEmpty(ExtensionProperties["FlowAccum"])){
                m_flowacc = ExtensionProperties["FlowAccum"];
            }
            if (!string.IsNullOrEmpty(ExtensionProperties["FlowDir"])){
                m_flowdir = ExtensionProperties["FlowDir"];
            }
            //if (!(ExtensionProperties["ExtractionLayers"] == null || ExtensionProperties["ExtractionLayers"].Length==0))
            //{
           //     m_extractionlayers = ExtensionProperties["ExtractionLayers"];
           // }

            //Container div and table.          
            System.Web.UI.HtmlControls.HtmlGenericControl propertiesDiv = new
                System.Web.UI.HtmlControls.HtmlGenericControl("propertiesDiv");
            propertiesDiv.Style[System.Web.UI.HtmlTextWriterStyle.Padding] = "10px";
            System.Web.UI.HtmlControls.HtmlTable table = new
                System.Web.UI.HtmlControls.HtmlTable();
            table.CellPadding = table.CellSpacing = 4;
            propertiesDiv.Controls.Add(table);
            // Header row.
            System.Web.UI.HtmlControls.HtmlTableRow row = new
                System.Web.UI.HtmlControls.HtmlTableRow();
            table.Rows.Add(row);
            System.Web.UI.HtmlControls.HtmlTableCell cell = new
                System.Web.UI.HtmlControls.HtmlTableCell();
            row.Cells.Add(cell);
            cell.ColSpan = 2;
            System.Web.UI.WebControls.Label lbl = new System.Web.UI.WebControls.Label();
            lbl.Text = "Choose the flow accumulation and flow direction layers.";
            cell.Controls.Add(lbl);
            // Flow Acc Layer drop-down row.
            row = new System.Web.UI.HtmlControls.HtmlTableRow();
            table.Rows.Add(row);
            cell = new System.Web.UI.HtmlControls.HtmlTableCell();
            row.Cells.Add(cell);
            lbl = new System.Web.UI.WebControls.Label();
            cell.Controls.Add(lbl);
            lbl.Text = "Flow Accumulation:";
            cell = new System.Web.UI.HtmlControls.HtmlTableCell();
            row.Cells.Add(cell);
            cell.Controls.Add(m_FlowAccDropDown);
            m_FlowAccDropDown.ID = "flowAccDropDown";
            // Wire the OnLayerChanged JavaScript function (defined in SupportingJavaScript) to fire when a new layer is selected.
            m_FlowAccDropDown.Attributes["onchange"] =
                "ExtensionConfigurator.OnLayerChanged(this);";
            // Flow dir layer drop-down row.
            row = new System.Web.UI.HtmlControls.HtmlTableRow();
            table.Rows.Add(row);
            cell = new System.Web.UI.HtmlControls.HtmlTableCell();
            row.Cells.Add(cell);
            lbl = new System.Web.UI.WebControls.Label();
            cell.Controls.Add(lbl);
            lbl.Text = "Flow Direction:";
            cell = new System.Web.UI.HtmlControls.HtmlTableCell();
            row.Cells.Add(cell);
            cell.Controls.Add(m_FlowDirDropDown);
            m_FlowDirDropDown.ID = "flowDirDropDown";
            // Get the path of the underlying map document and use it to populate the properties drop-downs.
            string fileName = ServerObjectProperties["FilePath"];
            populateDropDowns(serverContext, fileName);
            // Render and return the HTML for the container div.
            System.IO.StringWriter stringWriter = new System.IO.StringWriter();
            System.Web.UI.HtmlTextWriter htmlWriter = new System.Web.UI.HtmlTextWriter
                (stringWriter);
            propertiesDiv.RenderControl(htmlWriter);
            string html = stringWriter.ToString();
            stringWriter.Close();
            return html;
        }
        private void populateDropDowns(ESRI.ArcGIS.Server.IServerContext serverContext,
                               string mapDocPath)
        {
            logger.LogMessage(ServerLogger.msgType.warning, "SOE manager page", 8000,
                         "SOE Manager page: populateDropDowns");
               
            ESRI.ArcGIS.Carto.IMapServer3 mapServer = (ESRI.ArcGIS.Carto.IMapServer3)
                serverContext.ServerObject;
            string mapName = mapServer.DefaultMapName;
            // Using IMapServerDataAccess to get the data allows you to support MSD-based services.
            ESRI.ArcGIS.Carto.IMapServerDataAccess dataAccess =
                (ESRI.ArcGIS.Carto.IMapServerDataAccess)mapServer;
            ESRI.ArcGIS.Carto.IMapLayerInfo layerInfo;
            ESRI.ArcGIS.Carto.IMapLayerInfos layerInfos = mapServer.GetServerInfo
                (mapName).MapLayerInfos;
            Dictionary<string, List<string>> layersAndFieldsDictionary = new
                Dictionary<string, List<string>>();
           // bool addFields = false;
            // Loop through all layers.
            int c = layerInfos.Count;
            for (int i = 0; i < c; i++)
            {
                layerInfo = layerInfos.get_Element(i);
                logger.LogMessage(ServerLogger.msgType.warning, "SOE manager page", 8000,
                          "Layer "+layerInfo.Name+" has type "+layerInfo.Type);
               
                if (layerInfo.Type == "Raster")
                {
                    if (dataAccess.GetDataSource(mapName, i) as IRaster != null)
                    {
                        m_FlowAccDropDown.Items.Add(layerInfo.Name);
                        m_FlowDirDropDown.Items.Add(layerInfo.Name);
                        m_ExtractionLayersDropDown.Items.Add(layerInfo.Name);
                    }
                }
                if (layerInfo.IsFeatureLayer == true)
                {
                    ESRI.ArcGIS.Geodatabase.IFeatureClass fc =
                        (ESRI.ArcGIS.Geodatabase.IFeatureClass)dataAccess.GetDataSource
                        (mapName, i);
                    List<string> fieldsList = new List<string>();
                    // Check whether the current layer is a simple polygon layer.
                    if (fc.ShapeType ==
                        ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon &&
                        fc.FeatureType ==
                        ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple)
                    {
                        // Add the layer to the layers drop-down.
                        // Check whether the fields drop-down should be initialized with fields from the current loop layer.
                        // Add each field to the fields list.
                        
                        // do stuff with non-raster layers
                    }
                }
            }
            // Serialize the dictionary containing the layer and field names to JSON.
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new
                System.Web.Script.Serialization.JavaScriptSerializer();
           // m_jsonServiceLayersAndFields = serializer.Serialize
             //   (layersAndFieldsDictionary);
            
            // If a flow acc layer is defined for the extension, select it in the relevant drop-down.
            if (m_flowacc != null)
                m_FlowAccDropDown.SelectedValue = m_flowacc;
            // If a flow dir layer is defined for the extension, select it in the relevant drop-down.
            if (m_flowdir != null)
                m_FlowDirDropDown.SelectedValue = m_flowdir;

        }

        public string SupportingJavaScript
        {
            get
            {
                return string.Format(@"
                ExtensionConfigurator.OnLayerChanged = function(flowAccDropDown){
                    {
                        var faLayerName = flowAccDropDown.options[flowAccDropDown.selectedIndex].value;
                        var fdLayerName = flowDirLayerName.options[flowDirDropDown.selectedIndex].value;
                    }
                }
                ");
            }
        }

        public List<string> HtmlElementIds
        {
            get
            {
                return new List<string>(new string[] { "flowAccDropDown", "flowDirDropDown" });
            }

        }

        public void SaveProperties(ESRI.ArcGIS.Server.IServerContext serverContext,
                                System.Collections.Specialized.NameValueCollection Request,
                                bool isEnabled,
                                out System.Collections.Specialized.NameValueCollection ExtensionProperties,
                                out System.Collections.Specialized.NameValueCollection InfoProperties)
        {
            ExtensionProperties = new System.Collections.Specialized.NameValueCollection();
            string faLayerName = Request["flowAccDropDown"];
            string fdLayerName = Request["flowDirDropDown"];
            if (!string.IsNullOrEmpty(faLayerName))
            {
                ExtensionProperties.Add("FlowAccum", faLayerName);
            }
            if (!string.IsNullOrEmpty(fdLayerName))
            {
                ExtensionProperties.Add("FlowDir", fdLayerName);
            }
            InfoProperties = new System.Collections.Specialized.NameValueCollection();
        }

    }
}
