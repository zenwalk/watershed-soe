using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WatershedSOE;

namespace WatershedSOE.ArcCatalog
{
    public partial class PropertyForm : Form
    {
        // store propertied for the flow accumulation and direction layers and optionally extent limiting features
        private string m_FlowDirLayerName;
        private string m_FlowAccLayerName;
        private string m_ExtentFeatureLayerName;
        // We need to configure how map layers are extracted and exposed as paramaters in the REST schema.
        // For raster layers we need to save whether a layer is to be exposed, and if so what parameter name to use for it
        // For feature layers we need to do that, plus whether it is to be summarised and if so by what fields
        
        // We need to store a structured object for each layer that will be used to populate / populated from the form's datagrid
        // BUT
        // We can only store string values in the properties (as far as I know - no documentation and too tedious to experiment)
        // So we will store the property as a delimited string like 
        // Layername|||Enabled|||Paramname|||Catfield|||Valfield ::: Layername|||Enabled|||Paramname|||Catfield|||Valfield
        // etc but convert this property into a dictionary of structured objects (keyed by layer name) when the property form loads
        private Dictionary <string,ExtractionLayerProperties> m_ExtractionLayerProperties = new Dictionary <string,ExtractionLayerProperties>();
        
        private bool m_initDir = false;
        private bool m_initAcc = false;
        private bool m_initExt = false;
        private bool m_GridIsInSyncWithProperties = false;
        private bool m_GetLayersFromMap = true;
        public PropertyForm()
        {
            InitializeComponent();
            System.Type type = System.Type.GetTypeFromCLSID(typeof
                (ESRI.ArcGIS.Framework.AppRefClass).GUID);
            // get a reference to arccatalog
            ESRI.ArcGIS.CatalogUI.IGxApplication gxApp = Activator.CreateInstance
                (type) as ESRI.ArcGIS.CatalogUI.IGxApplication;
            // get a reference to the map service being modified
            ESRI.ArcGIS.Catalog.IGxAGSObject gxAgsObj = gxApp.SelectedObject as
                ESRI.ArcGIS.Catalog.IGxAGSObject;
            // only enable combo boxes if map service is stopped
            if (gxAgsObj.Status != "Stopped")
            {
                ComboFlowDir.Enabled = false;
                ComboFlowAcc.Enabled = false;
                ComboExtentFeatures.Enabled = false;
                dataGridView1.Enabled = false;
                radioReadMap.Enabled = false;
                radioReadMap.Enabled = false;
            }

        }
        public int getHWnd()
        {
            return this.Handle.ToInt32();
        }
        internal ESRI.ArcGIS.Framework.IComPropertyPageSite PageSite
        {
            private get;
            set;
        }
        internal string FlowDirLayer
        {
            get
            {
                return m_FlowDirLayerName;
            }
            set
            {
                m_FlowDirLayerName = value;
            }
        }
        internal bool ReadLayersFromMap
        {
            get
            {
                return m_GetLayersFromMap;
            }
            set
            {
                m_GetLayersFromMap = value;
            }
        }
        internal string FlowAccLayer
        {
            get 
            {
                return m_FlowAccLayerName;
            }
            set
            {
                m_FlowAccLayerName = value;
            }
        }
        internal string ExtentFeatureLayer
        {
            get
            {
                return m_ExtentFeatureLayerName;
            }
            set
            {
                m_ExtentFeatureLayerName = value;
            }
        }
        internal string ExtractionProperties
        {
            // the code calling get / set will be looking for one string
            // we store it as a dictionary of structured objects keyed by the layer name, 
            // splitting / joining the stored string based on the delimiter ":::" between layers
            // each member represents a row for / from the datagrid with columns separated by 
            // "|||"
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (ExtractionLayerProperties ep in m_ExtractionLayerProperties.Values)
                {
                    
                    sb.Append(ep.ToString()).Append(":::");
                }
                string props = sb.ToString();
                return props;
            }
            set
            {
                List<string> extractionPropsStrings = value.Split(new string[]{":::"},StringSplitOptions.None).ToList();
                foreach (string extractionPropStr in extractionPropsStrings)
                {
                    string[] extractionPropArr = extractionPropStr.Split(new string[] { "|||" }, StringSplitOptions.None);
                    m_ExtractionLayerProperties.Clear();
                    ExtractionLayerProperties extractionProp = new ExtractionLayerProperties(
                        extractionPropArr[0], // layer name
                        Boolean.Parse(extractionPropArr[1]), // enabled
                        extractionPropArr[2], // param name
                        extractionPropArr[3], // extraction type
                        extractionPropArr[4], // cat field name
                        extractionPropArr[5], // val field name
                        extractionPropArr[6],  // meas field name
                        null,
                        null,
                        null
                    );
                    m_ExtractionLayerProperties.Add(extractionPropArr[0],extractionProp);
                }
            }
        }
        internal void SetMap(string filePath)
        {
            // open the map document
            ESRI.ArcGIS.Carto.IMapDocument mapDocument = new ESRI.ArcGIS.Carto.MapDocumentClass();
            mapDocument.Open(filePath, null);
            // get first map (data frame)
            ESRI.ArcGIS.Carto.IMap map = mapDocument.get_Map(0);
            // track whether we need to save properties
            string[] tTmp = new string[m_ExtractionLayerProperties.Keys.Count];
            m_ExtractionLayerProperties.Keys.CopyTo(tTmp,0);
            // we will remove items from this previously configured list and anything left we know is no longer in map, so remove
            // from saved properties
            List<string> tPreviouslyConfiguredLayers = tTmp.ToList<string>();
            // get raster layers
            ESRI.ArcGIS.esriSystem.UID rasterLayerId = new ESRI.ArcGIS.esriSystem.UIDClass();
            // GUID for a raster layer
            rasterLayerId.Value = "{D02371C7-35F7-11D2-B1F2-00C04F8EDEFF}";
            ESRI.ArcGIS.Carto.IEnumLayer rasterEnumLayer = map.get_Layers(rasterLayerId, true);
            
            int dirSelectedIndex = 0;
            int accSelectedIndex = 0;
            // set a default value of none
            // if either box is left at none then the SOE will know not to
            // expose the watershed operation
            ComboFlowDir.Items.Add("NONE");
            ComboFlowAcc.Items.Add("NONE");
            ESRI.ArcGIS.Carto.IRasterLayer rasterLayer = null;
            while ((rasterLayer = rasterEnumLayer.Next() as ESRI.ArcGIS.Carto.IRasterLayer) !=
                null)
            {
                
                ESRI.ArcGIS.DataSourcesRaster.IRasterProps tRasterProps = 
                    rasterLayer.Raster as ESRI.ArcGIS.DataSourcesRaster.IRasterProps;
                ESRI.ArcGIS.Geodatabase.rstPixelType tPixelType = tRasterProps.PixelType;
                // flow dir can only be an integer raster of short or long types
                if (tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_UCHAR ||
                    tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_SHORT ||
                    tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_ULONG ||
                    tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_LONG)
                {
                    ComboFlowDir.Items.Add(rasterLayer.Name);
                    ComboFlowAcc.Items.Add(rasterLayer.Name);
                }
                // flow acc can theoretically be floating point as far as i can see
                // albeit it won't normally be
                else if (tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_FLOAT ||
                    tPixelType == ESRI.ArcGIS.Geodatabase.rstPixelType.PT_DOUBLE)
                {
                    ComboFlowAcc.Items.Add(rasterLayer.Name);
                }
                if (rasterLayer.Name == m_FlowAccLayerName)
                {
                    accSelectedIndex = ComboFlowAcc.Items.Count - 1;
                }
                else if (rasterLayer.Name == m_FlowDirLayerName)
                {
                    dirSelectedIndex = ComboFlowDir.Items.Count - 1;
                }
                string tRasterType = "ContinuousRaster";
                if (tRasterProps.IsInteger)
                {
                    tRasterType = "CategoricalRaster";
                }
                                
                ExtractionLayerProperties tLayerInMap = new ExtractionLayerProperties(rasterLayer.Name, false, rasterLayer.Name.Substring(0, 6),
                    tRasterType, "NONE", "NONE", "NONE",null,null,null);
                ExtractionLayerProperties tCurrentConfigForLayer;
                bool alreadyconfigured = m_ExtractionLayerProperties.TryGetValue(rasterLayer.Name,out tCurrentConfigForLayer);
                if (alreadyconfigured)
                {
                    tLayerInMap.Enabled = tCurrentConfigForLayer.Enabled;
                    tLayerInMap.ParamName = tCurrentConfigForLayer.ParamName;
                    // that's all that needs doing for a raster raster
                }
                m_ExtractionLayerProperties[rasterLayer.Name] = tLayerInMap;
                tPreviouslyConfiguredLayers.Remove(rasterLayer.Name);
            }
            int extSelectedIndex = 0;
            ESRI.ArcGIS.esriSystem.UID featlyrId = new ESRI.ArcGIS.esriSystem.UIDClass();
            featlyrId.Value = "{E156D7E5-22AF-11D3-9F99-00C04F6BC78E}";
            ESRI.ArcGIS.Carto.IEnumLayer featureEnumLayer = map.get_Layers(featlyrId,true);
            ESRI.ArcGIS.Carto.IFeatureLayer featureLayer = null;
            while ((featureLayer = featureEnumLayer.Next() as ESRI.ArcGIS.Carto.IFeatureLayer) !=
                null)
            {
                if (featureLayer.FeatureClass.ShapeType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon &&
                    featureLayer.FeatureClass.FeatureType == ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple)
                {
                    ComboExtentFeatures.Items.Add(featureLayer.Name);
                }
                if (featureLayer.Name == m_ExtentFeatureLayerName)
                {
                    extSelectedIndex = ComboExtentFeatures.Items.Count - 1;
                }
                // now stuff for the extraction layer configuration
                List<string> catfields = new List<string>();
                List<string> valfields = new List<string>();
                List<string> measfields = new List<string>();
                catfields.Add("NONE");
                valfields.Add("NONE");
                measfields.Add("NONE");
                ESRI.ArcGIS.Geodatabase.IFields tFLFields = featureLayer.FeatureClass.Fields;
                
                for (int i = 0; i < tFLFields.FieldCount; i++)
                {
                    ESRI.ArcGIS.Geodatabase.IField tField = featureLayer.FeatureClass.Fields.get_Field(i);
                    ESRI.ArcGIS.Geodatabase.esriFieldType tFieldType = tField.Type;
                    switch (tFieldType)
                    {
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeDouble:
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeSingle:
                            valfields.Add(tField.Name);
                            measfields.Add(tField.Name);
                            break;
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeInteger:
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeSmallInteger:
                            valfields.Add(tField.Name);
                            catfields.Add(tField.Name);
                            measfields.Add(tField.Name);
                            break;
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeString:
                        case ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeDate:
                            catfields.Add(tField.Name);
                            break;
                    }
                }
                ExtractionLayerProperties tLayerInMap = new ExtractionLayerProperties(featureLayer.Name, false, featureLayer.Name.Substring(0, 6),
                   "FeatureLayer", "NONE", "NONE", "NONE",catfields,valfields,measfields);
                ExtractionLayerProperties tCurrentConfigForLayer;
                bool alreadyconfigured = m_ExtractionLayerProperties.TryGetValue(featureLayer.Name, out tCurrentConfigForLayer);
                if (alreadyconfigured)
                {
                    // it is already there - loaded from properties - we will make a new object anyway but will copy over the configured 
                    // parameter name, enabled, and selected fields.
                    tLayerInMap.Enabled = tCurrentConfigForLayer.Enabled;
                    tLayerInMap.ParamName = tCurrentConfigForLayer.ParamName;
                    if (tCurrentConfigForLayer.CategoryFieldName != "NONE" &&
                        featureLayer.FeatureClass.FindField(tCurrentConfigForLayer.CategoryFieldName) != -1)
                    {
                        tLayerInMap.CategoryFieldName = tCurrentConfigForLayer.CategoryFieldName;
                    }
                    if (tCurrentConfigForLayer.ValueFieldName != "NONE" &&
                        featureLayer.FeatureClass.FindField(tCurrentConfigForLayer.ValueFieldName) != -1)
                    {
                        tLayerInMap.ValueFieldName = tCurrentConfigForLayer.ValueFieldName;
                    }
                    if (tCurrentConfigForLayer.MeasureFieldName != "NONE" &&
                        featureLayer.FeatureClass.FindField(tCurrentConfigForLayer.ValueFieldName) != -1)
                    {
                        tLayerInMap.MeasureFieldName = tCurrentConfigForLayer.MeasureFieldName;
                    }
                }
                m_ExtractionLayerProperties[featureLayer.Name] = tLayerInMap;
                // now we need to delete from m_ExtractionLayerProperties any configs that haven't been found in the map
                tPreviouslyConfiguredLayers.Remove(featureLayer.Name);
            }
            foreach (string invalidLayerName in tPreviouslyConfiguredLayers)
            {
                m_ExtractionLayerProperties.Remove(invalidLayerName);
            }
            // now, m_extractionlayerproperties is a dictionary of extractionlayerproperties objects 
            // where the objects contain the values from the previously saved lot if they were there
            foreach (ExtractionLayerProperties rowdetails in m_ExtractionLayerProperties.Values)
            {
              //  string[] rowParams = new string[]{
              //      rowdetails.LayerName,rowdetails.Enabled.ToString(),rowdetails.ParamName,rowdetails.CategoryFieldName,rowdetails.ValueFieldName
               //     };
                dataGridView1.Rows.Add();
                int newRowIdx = dataGridView1.Rows.Count - 1;
                DataGridViewRow tRow = dataGridView1.Rows[newRowIdx];
                tRow.Cells["LayerName"].Value = rowdetails.LayerName;
                tRow.Cells["LayerAvail"].Value = rowdetails.Enabled.ToString();
                tRow.Cells["LayerParam"].Value = rowdetails.ParamName;
                if (rowdetails.ExtractionType == "FeatureLayer")
                {
                    DataGridViewComboBoxCell cfcell = (DataGridViewComboBoxCell)tRow.Cells["CatField"];
                    
                    foreach (object itemToAdd in rowdetails.PossibleCategoryFields)
                    {
                        cfcell.Items.Add(itemToAdd);
                    }
                    cfcell.Value = rowdetails.CategoryFieldName;
                    DataGridViewComboBoxCell valcell = (DataGridViewComboBoxCell)tRow.Cells["ValField"];
                    foreach (object itemToAdd in rowdetails.PossibleValueFields)
                    {
                        valcell.Items.Add(itemToAdd);
                    }
                    valcell.Value = rowdetails.ValueFieldName;
                    DataGridViewComboBoxCell meascell = (DataGridViewComboBoxCell)tRow.Cells["MeasField"];
                    foreach (object itemToAdd in rowdetails.PossibleMeasureFields)
                    {
                        meascell.Items.Add(itemToAdd);
                    }
                    meascell.Value = rowdetails.MeasureFieldName;
                    tRow.Cells["MeasField"].Value = rowdetails.MeasureFieldName;
                }
                else
                {
                    tRow.Cells["CatField"].Value = rowdetails.CategoryFieldName;
                    tRow.Cells["ValField"].Value = rowdetails.ValueFieldName;
                    tRow.Cells["MeasField"].Value = rowdetails.MeasureFieldName;
                    tRow.Cells["CatField"].ReadOnly = true;
                    tRow.Cells["ValField"].ReadOnly = true;
                    tRow.Cells["MeasField"].ReadOnly = true;
                }
                int rowIdx = dataGridView1.Rows.Add(tRow);
            }
            mapDocument.Close();
            mapDocument = null;
            map = null;
            ComboFlowAcc.SelectedIndex = accSelectedIndex;
            ComboFlowDir.SelectedIndex = dirSelectedIndex;
            ComboExtentFeatures.SelectedIndex = extSelectedIndex;
            m_initAcc = true;
            m_initDir = true;
            m_initExt = true;
            m_GridIsInSyncWithProperties = true;
            radioReadMap.Checked = m_GetLayersFromMap;
            dataGridView1.Enabled = radioReadMap.Checked;
        }
        internal void RecreateLayerConfigFromGrid()
        {
            if (m_GridIsInSyncWithProperties) return;
            m_ExtractionLayerProperties.Clear();
            foreach (DataGridViewRow tLayerRow in dataGridView1.Rows)
            {
                string tLayerName = tLayerRow.Cells["LayerName"].Value.ToString();
                bool tLayerAvail = Boolean.Parse(tLayerRow.Cells["LayerAvail"].Value.ToString());
                string tLayerParam = tLayerRow.Cells["LayerParam"].Value.ToString();
                string tCatField = tLayerRow.Cells["CatField"].Value.ToString();
                string tValField = tLayerRow.Cells["ValField"].Value.ToString();
                string tMeasField = tLayerRow.Cells["MeasField"].Value.ToString();
                ExtractionLayerProperties tExtProps = new ExtractionLayerProperties(
                    tLayerName, tLayerAvail, tLayerParam, null, tCatField, tValField, tMeasField, null, null, null);
                m_ExtractionLayerProperties.Add(tLayerName,tExtProps);
            }
            m_GridIsInSyncWithProperties = true;
        }
        private void PropertyForm_Load(object sender, EventArgs e)
        {

        }
        internal void EnableGrid()
        {
            dataGridView1.Enabled = true;
            m_GetLayersFromMap = false;
        }
        internal void DisableGrid()
        {
            dataGridView1.Enabled = false;
            m_GetLayersFromMap = true;
        }
        private void ComboFlowDir_SelectedIndexChanged(object sender, EventArgs e)
        {
            //int selectedIndex = 0;
            m_FlowDirLayerName = ComboFlowDir.Text;
            if (m_initDir) { m_initDir = false; }
            else
            {
                this.PageSite.PageChanged();
            }
        }
        
        private void ComboFlowAcc_SelectedIndexChanged(object sender, EventArgs e)
        {
           // int selectedIndex = 0;
            m_FlowAccLayerName = ComboFlowAcc.Text;
            if (m_initAcc) { m_initAcc = false; }
            else { this.PageSite.PageChanged(); }
        }

        private void ComboExtentFeatures_SelectedIndexChanged(object sender, EventArgs e)
        {
           // int selectedIndex = 0;
            m_ExtentFeatureLayerName = ComboExtentFeatures.Text;
            if (m_initExt) { m_initExt = false; }
            else { this.PageSite.PageChanged(); }
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // set a flag to indicate that when the form is dismissed it will need to rebuild
            // m_ExtractionLayerProperties from the cell values
            if (m_GridIsInSyncWithProperties) { m_GridIsInSyncWithProperties = false; }
            else { this.PageSite.PageChanged(); }
        }

     
        private void radioReadMap_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if (radioButton.Checked)
            {
                DisableGrid();
            }
            else
            {
                EnableGrid();
            }
            this.PageSite.PageChanged();
        }

        

        

       

       

       
    }
    internal struct ExtractionLayerProperties
    {
        private readonly string m_extractionType;
        private readonly string m_layerName;
        private string m_paramName;
        private bool m_enabled;
        private string m_categoryField;
        private string m_valueField;
        private string m_measureField;
        private List<string> m_PossibleCategoryFields;
        private List<string> m_PossibleValueFields;
        private List<string> m_PossibleMeasureFields;
        // accessors
        internal string LayerName { get { return m_layerName; } }
        internal string ExtractionType { get { return m_extractionType; } }
        internal string ParamName { get { return m_paramName; } set { m_paramName = value; } }
        internal bool Enabled { get { return m_enabled; } set { m_enabled = value; } }
        internal string CategoryFieldName { get { return m_categoryField; } set { m_paramName = value; } }
        internal string ValueFieldName { get { return m_valueField; } set { m_paramName = value; } }
        internal string MeasureFieldName { get { return m_measureField; } set { m_paramName = value; } }
        internal List<string> PossibleCategoryFields { get { return m_PossibleCategoryFields; } set { m_PossibleCategoryFields = value; } }
        internal List<string> PossibleValueFields { get { return m_PossibleValueFields; } set { m_PossibleValueFields = value; } }
        internal List<string> PossibleMeasureFields { get { return m_PossibleMeasureFields; } set { m_PossibleMeasureFields = value; } }

        internal bool HasCategories { get { return m_categoryField != ""; } }
        internal bool HasValues { get { return m_valueField != ""; } }
        internal bool HasMeasures { get { return m_measureField != ""; } }

        public ExtractionLayerProperties(string layername, bool enabled, string paramname, string extractiontype, 
                string CategoryFieldName, string ValueFieldName, string MeasureFieldName,
                List<string>PossibleCategoryFields,List<string>PossibleValueFields,List<string> PossibleMeasureFields)
        {
            // this.m_layerType = layertype;
            this.m_layerName = layername;
            this.m_extractionType = extractiontype;
            this.m_enabled = enabled;
            this.m_paramName = paramname;
            this.m_categoryField = CategoryFieldName;
            this.m_valueField = ValueFieldName;
            this.m_measureField = MeasureFieldName;
            this.m_PossibleCategoryFields = PossibleCategoryFields;
            this.m_PossibleValueFields = PossibleValueFields;
            this.m_PossibleMeasureFields = PossibleMeasureFields;
        }
        public override string ToString()
        {
            return this.m_layerName + "|||" + this.m_enabled.ToString() + "|||" + this.m_paramName + "|||" + this.m_extractionType + "|||" +
                this.m_categoryField + "|||" + this.m_valueField + "|||" + this.m_measureField;
        }
    }
    internal enum ExtractionTypes
    {
        CategoricalRaster,
        ContinuousRaster,
        Features
    }
  
    
}
