using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WatershedSOE.ArcCatalog
{
    public partial class PropertyForm : Form
    {
        private string m_FlowDirLayerName;
        private string m_FlowAccLayerName;
        private string m_ExtentFeatureLayerName;

        private bool m_initDir = false;
        private bool m_initAcc = false;
        private bool m_initExt = false;

        
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
        internal void SetMap(string filePath)
        {
            // open the map document
            ESRI.ArcGIS.Carto.IMapDocument mapDocument = new ESRI.ArcGIS.Carto.MapDocumentClass();
            mapDocument.Open(filePath, null);
            // get first map (data frame)
            ESRI.ArcGIS.Carto.IMap map = mapDocument.get_Map(0);
            // get raster layers
            ESRI.ArcGIS.esriSystem.UID rasterLayerId = new ESRI.ArcGIS.esriSystem.UIDClass();
            // GUID for a raster layer
            rasterLayerId.Value = "{D02371C7-35F7-11D2-B1F2-00C04F8EDEFF}";
            ESRI.ArcGIS.Carto.IEnumLayer rasterEnumLayer = map.get_Layers(rasterLayerId, true);
            int dirSelectedIndex = 0;
            int accSelectedIndex = 0;
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

            }
            mapDocument.Close();
            mapDocument = null;
            map = null;
            m_initAcc = true;
            m_initDir = true;
            m_initExt = true;
            ComboFlowAcc.SelectedIndex = accSelectedIndex;
            ComboFlowDir.SelectedIndex = dirSelectedIndex;
            ComboExtentFeatures.SelectedIndex = extSelectedIndex;
        }
        
        private void PropertyForm_Load(object sender, EventArgs e)
        {

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

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
