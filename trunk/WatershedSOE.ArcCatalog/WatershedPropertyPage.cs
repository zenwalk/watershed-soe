namespace WatershedSOE.ArcCatalog 
{
    [System.Runtime.InteropServices.Guid("2EAD4A98-BB8C-4b88-A323-48F53653ACBF")]
    public class WatershedPropertyPage : SOEUtilities.SOEPropertyPage
    {
        private PropertyForm watershedPropertyPage;
        private string m_serverObjectType;
        private string m_extensionType;
        private ESRI.ArcGIS.esriSystem.IPropertySet m_serverObjectProperties;
        private ESRI.ArcGIS.esriSystem.IPropertySet m_extensionProperties;
        public WatershedPropertyPage()
        {
            watershedPropertyPage = new PropertyForm();
            m_serverObjectType = "MapServer";
            m_extensionType = "WatershedSOE";
        }
        ~WatershedPropertyPage()
        {
            // destructor function
            watershedPropertyPage.Dispose();
            watershedPropertyPage = null;
        }
        
        public override ESRI.ArcGIS.esriSystem.IPropertySet ServerObjectProperties
        {
            get
            {
                return m_serverObjectProperties;
            }
            set
            {
                m_serverObjectProperties = value;
                watershedPropertyPage.SetMap(m_serverObjectProperties.GetProperty("FilePath").ToString());
                
            }
        }
        public override ESRI.ArcGIS.esriSystem.IPropertySet ExtensionProperties
        {
            get
            {
                m_extensionProperties.SetProperty("FlowAccLayer", watershedPropertyPage.FlowAccLayer);
                m_extensionProperties.SetProperty("FlowDirLayer", watershedPropertyPage.FlowDirLayer);
                m_extensionProperties.SetProperty("ExtentFeatureLayer", watershedPropertyPage.ExtentFeatureLayer);
                // we need to update the ExtractionProperties of the property page to reflect the layer configuration
                m_extensionProperties.SetProperty("ReadConfigFromMap", watershedPropertyPage.ReadLayersFromMap.ToString());
                watershedPropertyPage.RecreateLayerConfigFromGrid();
                // this will call the get method on the property page's ExtractionProperties field. This in turn will call
                // tostring on each individual ExtractionLayerProperties, returning a |||-delimited string from each, and concatenate
                // those with a ::: delimiter. We save this returned string using the built in stuff
                m_extensionProperties.SetProperty("LayerConfiguration", watershedPropertyPage.ExtractionProperties);
                return m_extensionProperties;
            }
            set
            {
                m_extensionProperties = value;
                watershedPropertyPage.FlowAccLayer = m_extensionProperties.GetProperty("FlowAccLayer").ToString();
                watershedPropertyPage.FlowDirLayer = m_extensionProperties.GetProperty("FlowDirLayer").ToString();
                watershedPropertyPage.ExtentFeatureLayer = m_extensionProperties.GetProperty("ExtentFeatureLayer").ToString();
                watershedPropertyPage.ReadLayersFromMap = System.Boolean.Parse(m_extensionProperties.GetProperty("ReadConfigFromMap").ToString());
                // this will call the set method on the property page's extractionproperties field. This will split the string
                // up and build a new dictionary of ExtractionLayerProperties values.
                watershedPropertyPage.ExtractionProperties = m_extensionProperties.GetProperty("LayerConfiguration").ToString();
            }
        }
        public override int Height
        {
            get
            {
                //return base.Height;
                return 405;
            }
        }
        public override string ServerObjectExtensionType
        {
            get { return m_extensionType; }
        }
        public override string ServerObjectType
        {
            get { return m_serverObjectType; }
        }
        public override ESRI.ArcGIS.Framework.IComPropertyPageSite PageSite
        {
            set { watershedPropertyPage.PageSite = value; }
        }
        public override int Activate()
        {
            return watershedPropertyPage.getHWnd();
        }
        public override void Show()
        {
            watershedPropertyPage.Show();
        }
        public override void Hide()
        {
            watershedPropertyPage.Hide();
        }

    }
}
