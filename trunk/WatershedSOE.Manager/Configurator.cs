using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
// TODO needs to reference ESRI.ARcGIS.ServerManager but this requires web applications part of AGS
namespace WatershedSOE.Manager
{
    public class Configurator : ESRI.ArcGIS.ServerManager.IServerObjectExtensionConfigurator
    {
        private System.Web.UI.WebControls.DropDownList m_FlowAccDropDown = new
            System.Web.UI.WebControls.DropDownList();
        private System.Web.UI.WebControls.DropDownList m_FlowDirDropDown = new
            System.Web.UI.WebControls.DropDownList();
        private string m_flowacc;
        private string m_flowdir;
        private string m_jsonServiceLayers = "{}";

        public string LoadConfigurator(ESRI.ArcGIS.Server.IServerContext serverContext,
                               System.Collections.Specialized.NameValueCollection ServerObjectProperties, System.Collections.Specialized.NameValueCollection ExtensionProperties, System.Collections.Specialized.NameValueCollection InfoProperties, bool isEnabled, string servicesEndPoint, string serviceName, string serviceTypeName)
        {
            // Just return a message if the SOE is not enabled on the current service.
            if (!isEnabled)
                return ("<span>No Properties to configure</span>");
            // Initialize member variables holding the SOE's properties.
            if (!string.IsNullOrEmpty(ExtensionProperties["FlowAccum"]))
                m_flowacc = ExtensionProperties["FlowAccum"];
            if (!string.IsNullOrEmpty(ExtensionProperties["FlowDir"]))
                m_flowdir = ExtensionProperties["FlowDir"];
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
            m_FlowDirDropDown.ID = "flowAccDropDown";
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
            bool addFields = false;
            // Loop through all layers.
            int c = layerInfos.Count;
            for (int i = 0; i < c; i++)
            {
                layerInfo = layerInfos.get_Element(i);
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
                        m_layersDropDown.Items.Add(layerInfo.Name);
                        // Check whether the fields drop-down should be initialized with fields from the current loop layer.
                        if (layerInfo.Name == m_layer || (m_layer == null &&
                            m_layersDropDown.Items.Count == 1))
                            addFields = true;
                        // Add each field to the fields list.
                        ESRI.ArcGIS.Geodatabase.IFields fields = fc.Fields;
                        for (int j = 0; j < fields.FieldCount; j++)
                        {
                            ESRI.ArcGIS.Geodatabase.IField field = fields.get_Field(j);
                            fieldsList.Add(field.Name);
                            // If the current loop layer is the first, add its fields to the fields drop-down.                            
                            if (addFields)
                                m_fieldsDropDown.Items.Add(field.Name);
                        }
                        addFields = false;
                        // Add the layer name and its fields to the dictionary.
                        layersAndFieldsDictionary.Add(layerInfo.Name, fieldsList);
                    }
                }
            }
            // Serialize the dictionary containing the layer and field names to JSON.
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new
                System.Web.Script.Serialization.JavaScriptSerializer();
            m_jsonServiceLayersAndFields = serializer.Serialize
                (layersAndFieldsDictionary);
            // If a layer is defined for the extension, select it in the layers drop-down.
            if (m_layer != null)
                m_layersDropDown.SelectedValue = m_layer;
            // If a field is defined for the extension, select it in the fields drop-down.
            if (m_field != null)
                m_fieldsDropDown.SelectedValue = m_field;

        }
    }
}
