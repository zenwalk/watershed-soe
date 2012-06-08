using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Specialized;

using System.Runtime.InteropServices;
using System.EnterpriseServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.SpatialAnalyst;
using ESRI.ArcGIS.GeoAnalyst;
//TODO: sign the project (project properties > signing tab > sign the assembly)
//      this is strongly suggested if the dll will be registered using regasm.exe <your>.dll /codebase


namespace WatershedSOE
{
    [ComVisible(true)]
    [Guid("05c4b2b2-b64d-483d-9ce9-19e86911eac0")]
    [ClassInterface(ClassInterfaceType.None)]
    public class WatershedSOE : ServicedComponent, IServerObjectExtension, IObjectConstruct, IRESTRequestHandler
    {
        private string soe_name;

        private IPropertySet configProps;
        private IServerObjectHelper serverObjectHelper;
        private ServerLogger logger;
        private IRESTRequestHandler reqHandler;

        // variables to do with configuring the watershed operation data sources from property page
        private string m_FlowAccLayerName;
        private string m_FlowDirLayerName;
        private string m_ExtentFeatureLayerName;
        // datasets of the layers given above
        private IGeoDataset m_FlowDirDataset;
        private IGeoDataset m_FlowAccDataset;
        private IGeoDataset m_ExtentFeatureDataset;
        // true if both flow acc and flow dir layers are found
        // otherwise can only extract by input polygon (watershed operation not added to REST schema)
        private bool m_CanDoWatershed;
        private bool m_BuildLayerParamsFromMap;

        // list of extraction configuration objects: one for each extraction layer available in the map
        private List<ExtractionLayerConfig> m_ExtractableParams = new List<ExtractionLayerConfig>();
        
        public WatershedSOE()
        {
            soe_name = "WatershedSOE";
            logger = new ServerLogger();
            logger.LogMessage(ServerLogger.msgType.infoStandard,"startup",8000,"soe_name is "+soe_name);
        }

        #region IServerObjectExtension Members
        public void Init(IServerObjectHelper pSOH)
        {
            serverObjectHelper = pSOH;
        }

        public void Shutdown()
        {
            logger.LogMessage(ServerLogger.msgType.infoStandard,"Shutdown",8000,"Shutting down the watershed SOE");
            soe_name=null;
            serverObjectHelper=null;
        }
        #endregion

        #region IObjectConstruct Members

        public void Construct(IPropertySet props)
        {
            try
            {
                logger.LogMessage(ServerLogger.msgType.infoDetailed, "Construct", 8000, "Watershed SOE constructor running");
                object tProperty = null;
                m_CanDoWatershed = true;
                // IPropertySet doesn't have anything like a trygetvalue method
                // so if we don't know if a property will be present we have to just try getting
                // it and if there is an exception assumes it wasn't there
                try {
                    tProperty = props.GetProperty("FlowAccLayer");
                    if (tProperty as string == "None")
                    {
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flow accumulation layer set to 'None'. No watershed functionality.");
                        m_CanDoWatershed = false;
                        //throw new ArgumentNullException();
                    }
                    else
                    {
                        m_FlowAccLayerName = tProperty as string;
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Flow Accumulation layer: " + m_FlowAccLayerName);
                    }
                }
                catch{
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flow accumulation layer not set. No watershed functionality.");
                    m_CanDoWatershed = false;
                    //throw new ArgumentNullException();
                }
                try {
                    tProperty = props.GetProperty("FlowDirLayer");
                    if (tProperty as string == "None")
                    {
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flow direction layer set to 'None'. No watershed functionality.");
                        m_CanDoWatershed = false;
                    }
                    else
                    {
                        m_FlowDirLayerName = tProperty as string;
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Flow direction layer: " + m_FlowDirLayerName);
                    }
                }
                catch {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flow direction layer not set. No watershed functionality.");
                    m_CanDoWatershed = false;
                }
                try
                {
                    tProperty = props.GetProperty("ExtentFeatureLayer") as string;
                    if (tProperty as string =="None"){
                        logger.LogMessage(ServerLogger.msgType.debug, "Construct", 8000, 
                            "WSH: No extent features configured. Extent may still be passed as input");
                    }
                    else
                    {
                        m_ExtentFeatureLayerName = tProperty as string;
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Extent Feature layer: " + m_ExtentFeatureLayerName);
                    }
                }
                catch {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: no definition for extent feature layers found. Extent may still be passed as input");
                }

                try
                {
                    tProperty = props.GetProperty("ReadConfigFromMap");
                    if (tProperty == null || tProperty as string != "False")
                    {
                        m_BuildLayerParamsFromMap = true;
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: layer parameters will be built from map document layers");
                    }
                       
                    else
                    {
                        m_BuildLayerParamsFromMap = false;
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: layer parameters would be read from properties file but this is NOT IMPLEMENTED YET ");
                        // TODO: add code to read in LayerConfiguration parameter and parse it
                    }
                }
                catch
                {
                    m_BuildLayerParamsFromMap = true;
                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 8000, 
                        "WSH: no property found for ReadConfigFromMap; "+
                        "layer parameters will be built from map document layers");
                }
            }
            catch (Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Properties constructor threw an exception");
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, e.Message);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, e.ToString());
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, e.TargetSite.Name);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, e.StackTrace);
            }
            
            try
            {
                // get the datasets associated with the configured inputs to watershed delineation. 
                // Also note the other layers: we will make all others available for extraction
                // but need to note the data type and how the layer name should translate into a REST operation
                // parameter. This information will be stored in an ExtractionLayerConfig opbject for each layer
                // We only need to do this at startup not each time
                IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
                string mapName = mapServer.DefaultMapName;
                IMapLayerInfo layerInfo;
                IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
                ILayerDescriptions layerDescriptions = mapServer.GetServerInfo(mapName).DefaultMapDescription.LayerDescriptions;
                IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
                
                int c = layerInfos.Count;
                int acc_layerIndex=0;
                int dir_layerIndex=0;
                int ext_layerIndex=0;
                //Dictionary<int,string> other_layerIndices = new Dictionary<int,string>();
                List<string> tAllParams = new List<string>();
                for (int i=0;i<c;i++)
                {
                    layerInfo = layerInfos.get_Element(i);
                    if(m_CanDoWatershed && layerInfo.Name == m_FlowAccLayerName)
                    {
                        acc_layerIndex = i;
                    }
                    else if (m_CanDoWatershed && layerInfo.Name == m_FlowDirLayerName)
                    {
                        dir_layerIndex = i;
                    }
                    else if (m_CanDoWatershed && m_ExtentFeatureLayerName != null && layerInfo.Name == m_ExtentFeatureLayerName)
                    {
                        ext_layerIndex = i;
                    }
                    else if (m_BuildLayerParamsFromMap)
                    // note the else if is deliberately arranged so that layers used for watershed extraction
                    // won't be exposed as extractable
                    {
                        // Types appear to be "Raster Layer", "Feature Layer", and "Group Layer"
                        logger.LogMessage(ServerLogger.msgType.debug,
                            "Construct", 8000,
                            "WSH: processing extractable map layer " + layerInfo.Name + " at ID " +
                            layerInfo.ID + " of type " + layerInfo.Type);
                        
                        if (layerInfo.Type == "Raster Layer" || layerInfo.Type == "Feature Layer")
                        {
                            string tName = layerInfo.Name;
                            string tDesc = layerInfo.Description;
                            if (tName.IndexOf(':') == -1 && tDesc.IndexOf(':') == -1)
                            {
                                // fail if any of the map layers except the ones used for the catchment definition
                                // don't have a name or description starting with 6 or less characters followed by :
                                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000,
                                     " Watershed SOE warning: could determine output parameter string for layer " + tName +
                                     " and it will not be available for extraction. " +
                                     " Ensure that either the layer name or description starts with an ID for the " +
                                     " service parameter name to be exposed, max 6 characters and separated by ':'" +
                                     " e.g. 'LCM2K:Land Cover Map 2000'");
                                continue;
                            }
                            else if (tName.IndexOf(':') > 5 && tDesc.IndexOf(':') > 5)
                            {
                                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000,
                                     " Watershed SOE warning: read output parameter string for layer " + tName +
                                     " but it was too long." +
                                     " Ensure that either the layer name or description starts with an ID for the " +
                                     " service parameter name to be exposed, max 6 characters and separated by ':'" +
                                     " e.g. 'LCM2K:Land Cover Map 2000'. Layer will not be available for extraction.");
                                continue;
                            }
                            string tParamName;
                            string tProcessedName;
                            if (tName.IndexOf(':') != -1)
                            {
                                tParamName = tName.Substring(0, tName.IndexOf(':'));
                                tProcessedName = tName.Substring(tName.IndexOf(':')+1).Trim();
                            }
                            else
                            {
                                tParamName = tDesc.Substring(0, tDesc.IndexOf(':'));
                                tProcessedName = tName.Trim();
                            }
                            if (tAllParams.Contains(tParamName))
                            {
                                logger.LogMessage(ServerLogger.msgType.error,"Construct",800,
                                    "Watershed SOE warning: duplicate parameter name found for layer "+tName +
                                    "(parameter "+tParamName+" is set on another map layer). Layer will not be available"+
                                    " for extraction.");
                                continue;
                            }
                            else{
                                tAllParams.Add(tParamName);
                            }
                            string tDescription = "";
                            if (layerInfo.Description.Length > 0)
                            {
                                if (layerInfo.Description.IndexOf(':') == -1)
                                {
                                    tDescription = layerInfo.Description.Trim();
                                }
                                else if (layerInfo.Description.IndexOf(':') < 6)
                                {
                                    tDescription = layerInfo.Description.Substring(layerInfo.Description.IndexOf(':') + 1).Trim();
                                }
                                else
                                {
                                    tDescription = layerInfo.Description.Trim();
                                }
                            }
                          
                            ExtractionTypes tExtractionType = ExtractionTypes.Ignore;
                            if (layerInfo.Type == "Raster Layer")
                            {
                                // determine whether we will summarise the raster layer "categorically"
                                // i.e. a count of each value, or "continuously" i.e. min/max/avg statistics
                                // based on how the raster is symbolised in the map and whether or not
                                // it is of integer type
                                
                                // TODO : Also store the labels for classes in categorical rasters
                                // so that these can be returned by the SOE to the client
                                // Cast the renderer to ILegendInfo, get ILegendGroup from it and each 
                                // ILegendClass from that to get the string Label

                                // Get renderer
                                // THIS ONLY WORKS WITH MXD SERVICES: WE CANNOT DO THIS ON AN MSD BASED SERVICE
                                IMapServerObjects3 tMapServerObjects = mapServer as IMapServerObjects3;
                                ILayer tLayer = tMapServerObjects.get_Layer(mapName, i);
                                IRasterLayer tRasterLayer = (IRasterLayer)tLayer;
                                IRasterRenderer tRasterRenderer = tRasterLayer.Renderer;
                                // Get raster data
                                IRaster tRaster = dataAccess.GetDataSource(mapName, i) as IRaster;
                                IRasterProps tRasterProps = tRaster as IRasterProps;
                                IGeoDataset tRasterGDS = tRaster as IGeoDataset;

                                bool tTreatAsCategorical = false;
                                if (tRasterRenderer is RasterUniqueValueRenderer)
                                {
                                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 800,
                                   "Raster layer " + tName +
                                   "is symbolised by unique values - treating layer as categorical");
                                    tTreatAsCategorical = tRasterProps.IsInteger;
                                }
                                else if (tRasterRenderer is RasterDiscreteColorRenderer)
                                {
                                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 800,
                                   "Raster layer " + tName +
                                   "is symbolised by discrete colours - treating layer as categorical");
                                    tTreatAsCategorical = tRasterProps.IsInteger;
                                }
                                else if (tRasterRenderer is RasterClassifyColorRampRenderer)
                                {
                                    // TODO - treat a classified colour ramp as categorical but categories
                                    // determined by classes rather than unique values... needs the summary
                                    // method to have access to the class breaks
                                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 800,
                                   "Raster layer " + tName +
                                   "is symbolised by classified groups - treating layer as continuous");
                                }
                                else if (tRasterRenderer is RasterStretchColorRampRenderer)
                                {
                                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 800,
                                   "Raster layer " + tName +
                                   "is symbolised by colour stretch - treating layer as continuous");
                                }
                                else
                                {
                                    logger.LogMessage(ServerLogger.msgType.debug, "Construct", 800,
                                   "Raster layer " + tName +
                                   "is symbolised with unsupported renderer - treating as continuous");
                                }
                                tExtractionType = tTreatAsCategorical?
                                    ExtractionTypes.CategoricalRaster:
                                    ExtractionTypes.ContinuousRaster;
                                ExtractionLayerConfig tLayerInfo = new ExtractionLayerConfig
                                    (i, tProcessedName,tDescription,tExtractionType, tParamName, -1, -1, -1,tRasterGDS);
                                m_ExtractableParams.Add(tLayerInfo);
                            }
                            else
                            {
                                // Feature class layer
                                // TODO - Get the category / values from the symbology (renderer) as for rasters

                                IFeatureClass tFC = dataAccess.GetDataSource(mapName, i) as IFeatureClass;
                                IGeoDataset tFeatureGDS = tFC as IGeoDataset;
                                esriGeometryType tFCType = tFC.ShapeType;
                                if (tFCType == esriGeometryType.esriGeometryPoint || tFCType == esriGeometryType.esriGeometryMultipoint)
                                {
                                    tExtractionType = ExtractionTypes.PointFeatures;
                                }
                                else if (tFCType == esriGeometryType.esriGeometryPolyline || tFCType == esriGeometryType.esriGeometryLine)
                                {
                                    tExtractionType = ExtractionTypes.LineFeatures;
                                }
                                else if (tFCType == esriGeometryType.esriGeometryPolygon)
                                {
                                    tExtractionType = ExtractionTypes.PolygonFeatures;
                                }
                                int tCategoryField = layerInfo.Fields.FindFieldByAliasName("CATEGORY");
                                int tValueField = layerInfo.Fields.FindFieldByAliasName("VALUE");
                                int tMeasureField = layerInfo.Fields.FindFieldByAliasName("MEASURE");
                                ExtractionLayerConfig tLayerInfo = new ExtractionLayerConfig
                                    (i,tProcessedName,tDescription, tExtractionType, tParamName, tCategoryField, tValueField, tMeasureField,tFeatureGDS);
                                m_ExtractableParams.Add(tLayerInfo);
                                // layers with any other geometry type will be ignored
                            }
                        }
                    }
                    else
                    {
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, 
                            "WSH: Code to build layer params from properties is not implemented. No extractable"+
                            "params will be available.");
                    }
                }
                IRaster tFDR = dataAccess.GetDataSource(mapName,dir_layerIndex) as IRaster;
                m_FlowDirDataset =  tFDR as IGeoDataset;
                IRaster tFAR = dataAccess.GetDataSource(mapName,acc_layerIndex) as IRaster;
                m_FlowAccDataset = tFAR as IGeoDataset;
                if(m_FlowDirDataset == null || m_FlowAccDataset == null)
                {
                    logger.LogMessage(ServerLogger.msgType.error,"Construct", 8000,"Watershed SOE Error: layer not found");
                    m_CanDoWatershed = false;
                   // return;
                }
                else 
                {
                    m_CanDoWatershed = true;
                }
                if (ext_layerIndex != 0)
                {
                    m_ExtentFeatureDataset = dataAccess.GetDataSource(mapName, ext_layerIndex) as IGeoDataset;
                }
            }
            catch (Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.error,"Construct",8000,"Watershed SOE error: could not get the datasets associated with configured map layers."+
                    "Exception: "+e.Message+e.Source+e.StackTrace+e.TargetSite);
            }
            try
            {
                reqHandler = new SoeRestImpl(soe_name, CreateRestSchema()) as IRESTRequestHandler;
            }
            catch (Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, "WSH: could not create REST schema. Exception: "+e.Message+ " "+e.Source+" "+e.StackTrace+" "+e.TargetSite);
         
            }
        
       }
        
        #endregion

        #region IRESTRequestHandler Members

        public string GetSchema()
        {
            return reqHandler.GetSchema();
        }

        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName, string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            return reqHandler.HandleRESTRequest(Capabilities, resourceName, operationName, operationInput, outputFormat, requestProperties, out responseProperties);
        }

        #endregion

        private RestResource CreateRestSchema()
        {
            RestResource rootRes = new RestResource(soe_name, false, RootResHandler);
            logger.LogMessage(ServerLogger.msgType.infoDetailed, "CreateRestSchema", 8000, 
                "WSH: attempting to create REST schema");
         
            // build the rest schema to reflect the layers available for extraction in this particular service
            IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
            string mapName = mapServer.DefaultMapName;
            IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
            List<string> tOptionalParams = new List<string>();
            foreach (ExtractionLayerConfig tExtractableLayer in m_ExtractableParams)
            {
                tOptionalParams.Add(tExtractableLayer.ParamName);
                
                    logger.LogMessage(ServerLogger.msgType.infoDetailed, "CreateRestSchema", 8000,
                        "WSH: added parameter "+tExtractableLayer.ParamName);
            }
            List<string> tPermanentParams = new List<string>(){
                "search_id","location","extent","input_wkid","extractToPolygonAttributes"};
            List<string> tAllParams = new List<string>();
            tAllParams.AddRange(tPermanentParams);
            tAllParams.AddRange(tOptionalParams);
            // RestOperation constructor takes: name, parameter(s), format(s), handler
            if (m_CanDoWatershed){
                RestOperation watershedOper = new RestOperation("createWatershed",
                                                      //new string[] { "hydroshed_id", "location", "extent", "lcm2k", "elev","totalupstream" },
                                                      tAllParams.ToArray(),
                                                      new string[] { "json" },
                                                      CreateWatershedHandler);
                
                rootRes.operations.Add(watershedOper);
                
            }
            List<string> tPolygonExtractionParams = new List<string>(){
                "search_id","polygon","input_wkid","extractToPolygonAttributes"};
            tPolygonExtractionParams.AddRange(tOptionalParams);
            RestOperation extractByPolygonOper = new RestOperation("extractByPolygon",
                                                    tPolygonExtractionParams.ToArray(),
                                                    new string[] { "json" },
                                                    ExtractByPolygonHandler);
            rootRes.operations.Add(extractByPolygonOper);
            RestOperation describeLayersOper = new RestOperation("describeLayers",
                                                   new string[0],
                                                   new string[] { "json" },
                                                   DescribeLayersHandler);
            rootRes.operations.Add(describeLayersOper);
            return rootRes;
        }

        private byte[] RootResHandler(NameValueCollection boundVariables, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;
            JsonObject result = new JsonObject();
            return Encoding.UTF8.GetBytes(result.ToJson());
        }
        #region REST Operation main handlers
        private byte[] DescribeLayersHandler(NameValueCollection boundVariables,
                                            JsonObject operationInput,
                                            string outputFormat,
                                            string requestProperties,
                                            out string responseProperties)
        {
            responseProperties = null;
            JsonObject tResult = new JsonObject();
            tResult.AddLong("AvailableLayerCount", m_ExtractableParams.Count);
            JsonObject tLayersJson = new JsonObject();
            foreach (ExtractionLayerConfig layer in m_ExtractableParams){
                //int id = layer.LayerID;
                string lyrName = layer.LayerName;
                string requestParameter = layer.ParamName;
                string extractionType = layer.ExtractionType.ToString();
                JsonObject tLayerJson = new JsonObject();
                //tLayerJson.AddLong("LayerId", id);
                tLayerJson.AddString("LayerName",lyrName);
                tLayerJson.AddString("LayerDescription", layer.LayerDescription);
                tLayerJson.AddString("ExtractionType", extractionType);
                if (layer.HasCategories)
                {
                    IFeatureClass tLayerAsFc = (IFeatureClass)layer.LayerDataset;
                    string tCatName = tLayerAsFc.Fields.get_Field(layer.CategoryField).Name;
                    tLayerJson.AddString("CategoryField", tCatName);
                }
                tLayersJson.AddObject(requestParameter,tLayerJson);
            }
            tResult.AddObject("Extractions", tLayersJson);
            byte[] tOutput = System.Text.Encoding.UTF8.GetBytes(tResult.ToJson());
            return tOutput;
        }
        private byte[] CreateWatershedHandler(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            responseProperties = null;

            #region Process the REST arguments
            // hydroshed_id - REQUIRED - to identify the overall result
            string search_id;
            bool found = operationInput.TryGetString("search_id", out search_id);
            if (!found || string.IsNullOrEmpty(search_id))
            {
                throw new ArgumentNullException("search_id");
            }
            // input point - REQUIRED - the search location
            JsonObject jsonPoint;
            found = operationInput.TryGetJsonObject("location", out jsonPoint);
            if (!found)
            {
                throw new ArgumentNullException("location");
            }
            IPoint locationpoint = Conversion.ToGeometry(jsonPoint, esriGeometryType.esriGeometryPoint) as IPoint;
            long? jsonWkid;
            found = operationInput.TryGetAsLong("input_wkid", out jsonWkid);
            if (!found)
            {
                throw new ArgumentNullException("input_wkid", "WKID numeric value for spatial reference of input point must be provided");
            }
            if (jsonWkid.HasValue)
            {
                int wkid = (int)jsonWkid.Value;
                ISpatialReferenceFactory2 tInSRFac = new SpatialReferenceEnvironment() as ISpatialReferenceFactory2;
                ISpatialReference tInSR = tInSRFac.CreateSpatialReference(wkid);
                locationpoint.SpatialReference = tInSR;
            }
            // extent - OPTIONAL - we will use full extent if not provided but this is slow!!
            // TODO maybe preferable to have the extent looked up in the SOE rather than expecting it as a parameter
            JsonObject jsonExtent;
            found = operationInput.TryGetJsonObject("extent", out jsonExtent);
            IGeometry tAnalysisEnvelope;
            if (found && jsonExtent != null)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "process input params", 8000, "Found input extent json object ");
                tAnalysisEnvelope = convertAnyJsonGeometry(jsonExtent);
                logger.LogMessage(ServerLogger.msgType.debug, "process input params", 8000, "Input extent processed ok ");
                try
                {
                    logger.LogMessage(ServerLogger.msgType.debug, "process input params", 8000, "Input extent height*width are: " + tAnalysisEnvelope.Envelope.Height.ToString() + " * " + tAnalysisEnvelope.Envelope.Width.ToString());
                }
                catch (NullReferenceException nre)
                {
                    logger.LogMessage(ServerLogger.msgType.debug, "Processing parameters", 8000, "Problem reading input extent, exception was " + nre.Message + " at " + nre.StackTrace);
                }
            }
            else
            {
                tAnalysisEnvelope = null;
                logger.LogMessage(ServerLogger.msgType.debug, "process input params", 8000, "No input extent parameter requested ");
            }
            List<ExtractionLayerConfig> extractionRequests = new List<ExtractionLayerConfig>();
            foreach (ExtractionLayerConfig tExtLayerInfo in m_ExtractableParams)
            {
                string jsonParam = tExtLayerInfo.ParamName;
                bool? wasRequested;
                found = operationInput.TryGetAsBoolean(jsonParam, out wasRequested);
                if (found && wasRequested.HasValue && (bool)wasRequested)
                {
                    extractionRequests.Add(tExtLayerInfo);
                }
            }
            // check whether to return as json structured object or all flattened onto attributes of the 
            // polygon
            bool returnAsPolygonAttributes=false;
            if (extractionRequests.Count > 0)
            {
                bool? nullableBool;
                found = operationInput.TryGetAsBoolean("extractToPolygonAttributes", out nullableBool);
                if (found && nullableBool.HasValue)
                {
                    returnAsPolygonAttributes = (bool)nullableBool;
                }
            }
            #endregion

            #region Do the actual watershed extraction
            // Modified the computeWatershed method to return both the raster and converted polygon versions of the 
            // watershed. Because the polygon version, if made by unioning separate polygons, is multipart, and 
            // although this is what we want to return to the user, the raster extraction operations can't handle
            // that so we run them with a raster mask input instead. Returning both here saves the extraction methods 
            // from converting back to a raster.
            IGeoDataset tWatershedPolyGDS;
            IGeoDataset tWatershedRasterGDS;
            if (tAnalysisEnvelope != null)
            {
                KeyValuePair<IGeoDataset, IGeoDataset> tPair = computeWatershed(locationpoint, tAnalysisEnvelope.Envelope);
                tWatershedPolyGDS = tPair.Value;
                tWatershedRasterGDS = tPair.Key;
            }
            else
            {
                try
                {
                    IEnvelope tAnalysisActuallyAnEnvelope = GetAnalysisEnvelope(locationpoint);
                    KeyValuePair<IGeoDataset, IGeoDataset> tPair = computeWatershed(locationpoint, tAnalysisActuallyAnEnvelope);
                    tWatershedPolyGDS = tPair.Value;
                    tWatershedRasterGDS = tPair.Key;
                }
                catch
                {
                    // error getting the extent. Compute watershed without one (will be slow).
                    KeyValuePair<IGeoDataset, IGeoDataset> tPair = computeWatershed(locationpoint, null);
                    tWatershedPolyGDS = tPair.Value;
                    tWatershedRasterGDS = tPair.Key;
                }
            }
            #endregion
            #region Modify the default fields in polygon catchment
            // raster-to-poly conversion adds some fields we don't want - remove them
            // also we will return the search id, and the corresponding outlet coordinates as 
            // attributes on the catchment
            try
            {
                IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
                // these get made by raster-poly conversion and they're boring
                TryDeleteAField(tPolygonAsFC, "GRIDCODE");
                TryDeleteAField(tPolygonAsFC, "ID");
                // Now then now then. After an irritating Thursday afternoon i've discovered that we can't use
                // tPolygonAsFC.GetFeature(1). Because 1 doesn't mean the first one in the FC, but the one with
                // OID = 1. If there were multiple polygons that got deleted and replaced by a single unioned one, then 
                // the unioned one won't have OID = 1, as OIDs aren't reused. So we have to use a cursor to get the 
                // feature instead. Add all the fields first, to save having to redo the feature retrieval from the 
                // cursor, then set them if they added ok.
                bool addedSearchId = AddAField(tPolygonAsFC, "search_id", esriFieldType.esriFieldTypeString, search_id.Length);
                bool addedOutletX = AddAField(tPolygonAsFC, "outlet_x", esriFieldType.esriFieldTypeDouble);
                bool addedOutletY = AddAField(tPolygonAsFC, "outlet_y", esriFieldType.esriFieldTypeDouble);
                IFeature tCatchmentFeature;
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                tCatchmentFeature = tFeatureCursor.NextFeature(); // there will only be one, not bothering with loop
                if (addedSearchId)
                {
                    try
                    {
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("search_id"), search_id);
                        tCatchmentFeature.Store();
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "error setting search id field with value " +
                            search_id + ". Detail: " + ex.StackTrace + " " + ex.Message);
                    }
                }
                if (addedOutletX)
                {
                    try
                    {
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("outlet_x"), locationpoint.X);
                        tCatchmentFeature.Store();
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "error setting outlet x field with value " +
                            locationpoint.X + ". Detail: " + ex.StackTrace + " " + ex.Message);
                    }
                }
                if (addedOutletY)
                {
                    try
                    {
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("outlet_y"), locationpoint.Y);
                        tCatchmentFeature.Store();
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "error setting outlet y field with value " +
                            locationpoint.Y + ". Detail: " + ex.StackTrace + " " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "some weird problem setting fields in output polygon" + ex.Message + ex.TargetSite.Name + ex.StackTrace + ex.InnerException.Message);
            }
            #endregion
            bool tPolygonIsMultipart = false;
            {
                IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                IFeature tCatchmentFeature = tFeatureCursor.NextFeature();
                IPolygon tCatchmentPolygon = (IPolygon)tCatchmentFeature.ShapeCopy;
                tPolygonIsMultipart = tCatchmentPolygon.ExteriorRingCount > 1;
            }
            logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "created watershed polygon, proceeding with extractions");
            #region Do the catchment characteristic extractions, if any
            // Use the catchment feature (both vector and original raster versions) as input into the generic 
            // extraction method
            // will return one with nothing in if there are no extraction requests
            ExtractionResultCollection tExtractionResults = 
                ProcessExtractions(search_id, tWatershedPolyGDS, tWatershedRasterGDS, extractionRequests);
            //IGeoDataset tFinalPolygonGDS = ProcessExtractions(tWatershedPolyGDS, tWatershedRasterGDS, extractionRequests);
            #endregion
            // The catchment feature now exists and we also have all the attributes requested. Ready to go.
            // Return either as attributes on the feature itself or as a structured JSON object
            if (returnAsPolygonAttributes)
            {
                IRecordSetInit returnRecSet = new RecordSetClass();
                IGeoDataset tFinalPolygonGDS = tExtractionResults.ResultsAsAttributedGeodataset;
                returnRecSet.SetSourceTable(tFinalPolygonGDS as ITable, null);
                IRecordSet recset = returnRecSet as IRecordSet;
                byte[] jsonFeatures = Conversion.ToJson(recset);
                return jsonFeatures;
            }
            else
            {
                JsonObject tResultsAsJson = tExtractionResults.ResultsAsJson;
                byte[] jsonFeatures = System.Text.Encoding.UTF8.GetBytes(tResultsAsJson.ToJson());
                return jsonFeatures;
            }
        }
        private byte[] ExtractByPolygonHandler(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            string search_id;
            bool found = operationInput.TryGetString("search_id", out search_id);
            if (!found || string.IsNullOrEmpty(search_id))
            {
                throw new ArgumentNullException("search_id");
            }
            // input polygon - REQUIRED - the polygon to summarise data within
            JsonObject jsonPolygon;
            found = operationInput.TryGetJsonObject("polygon", out jsonPolygon);
            if (!found)
            {
                throw new ArgumentNullException("polygon");
            }
            IPolygon extractionPolygon = Conversion.ToGeometry(jsonPolygon, esriGeometryType.esriGeometryPolygon) as IPolygon;
            long? jsonWkid;
            found = operationInput.TryGetAsLong("input_wkid", out jsonWkid);
            if (!found)
            {
                throw new ArgumentNullException("input_wkid", "WKID numeric value for spatial reference of input point must be provided");
            }
            if (jsonWkid.HasValue)
            {
                int wkid = (int)jsonWkid.Value;
                ISpatialReferenceFactory2 tInSRFac = new SpatialReferenceEnvironment() as ISpatialReferenceFactory2;
                ISpatialReference tInSR = tInSRFac.CreateSpatialReference(wkid);
                extractionPolygon.SpatialReference = tInSR;
            }
            else
            {
                // we won't get here
                extractionPolygon.SpatialReference = new UnknownCoordinateSystemClass();
            }
            bool? reqReturnAsAttributes;
            bool returnAsAttributes = false;
            found = operationInput.TryGetAsBoolean("extractToPolygonAttributes", out reqReturnAsAttributes);
            if (found && reqReturnAsAttributes.HasValue)
            {
                returnAsAttributes = (bool)reqReturnAsAttributes;
            }
            List<ExtractionLayerConfig> extractionRequests = new List<ExtractionLayerConfig>();
            foreach (ExtractionLayerConfig tExtLayerInfo in m_ExtractableParams)
            {
                string jsonParam = tExtLayerInfo.ParamName;
                bool? wasRequested;
                found = operationInput.TryGetAsBoolean(jsonParam, out wasRequested);
                if (found && wasRequested.HasValue && (bool)wasRequested)
                {
                    extractionRequests.Add(tExtLayerInfo);
                }
            }
            logger.LogMessage(ServerLogger.msgType.debug, "ExtractByPolygonHandler", 99,
                          "Processed inputs, attempting " + extractionRequests.Count.ToString() + " extractions");
            // now need to convert the IPolygon to a geodataset, (a polygon one) for feature
            // extractions.
            IWorkspace inMemWksp = CreateInMemoryWorkspace() as IWorkspace;
            IFeatureWorkspace inMemFeatWksp = inMemWksp as IFeatureWorkspace;
            IFeatureClass tPolyAsFC = CreateFeatureClassFromGeometry(extractionPolygon, inMemFeatWksp, extractionPolygon.SpatialReference.FactoryCode);
            IArea tArea = extractionPolygon as IArea;
            if (AddAField(tPolyAsFC,"Total_Area",esriFieldType.esriFieldTypeDouble))
            {
                IFeatureCursor tFCursor = tPolyAsFC.Search(null,false);
                IFeature tPolyAsFeature = tFCursor.NextFeature();
                tPolyAsFeature.set_Value(tPolyAsFC.FindField("Total_Area"),tArea.Area);
                tPolyAsFeature.Store();
            }
            IGeoDataset tPolygonGDS = tPolyAsFC as IGeoDataset;
            // now do the extractions from it
            ExtractionResultCollection tExtractionResults =
                ProcessExtractions(search_id,tPolygonGDS, null, extractionRequests);
            // Warning! Don't go assuming that the suggestively-named responseProperties can be set to anything 
            // helpful to describe, say, response properties. Try setting it to anything other than null 
            // (that I have tried) and you get "500 Unexpected Error" message and lose the best part of an 
            // afternoon working out why!
            //responseProperties = "Extractions processed successfully";
            responseProperties = null;
            logger.LogMessage(ServerLogger.msgType.debug, "ExtractByPolygonHandler", 99,
                          "Extractions complete, returning feature");
            if (returnAsAttributes)
            {
                IRecordSetInit returnRecSet = new RecordSetClass();
                IGeoDataset tFinalGDS = tExtractionResults.ResultsAsAttributedGeodataset;
                returnRecSet.SetSourceTable(tFinalGDS as ITable, null);
                IRecordSet recset = returnRecSet as IRecordSet;
                byte[] jsonFeature = Conversion.ToJson(recset);
                return jsonFeature;
            }
            else
            {
                JsonObject tResultsAsJson = tExtractionResults.ResultsAsJson;
                byte[] jsonFeatures = System.Text.Encoding.UTF8.GetBytes(tResultsAsJson.ToJson());
                return jsonFeatures;
            }
        }

        
            #endregion

        #region Main GIS stuff: create watershed, extract data from layers
        private KeyValuePair<IGeoDataset, IGeoDataset> computeWatershed(IPoint pour_point, IEnvelope analysisExtent)
        {
            try
            {
                //bodge the input point into its nasty shell of arcobjects junk for analysis
                IHydrologyOp pHydrologyOp = new RasterHydrologyOp() as IHydrologyOp;
                IPointCollection3 tPointCollection = new MultipointClass();
                object tPointlessMissingObject = Type.Missing;
                tPointCollection.AddPoint(pour_point, ref tPointlessMissingObject, ref tPointlessMissingObject);

                // open the accumulation and direction datasets, hardcoded
                //IGeoDataset tAccum = OpenRasterDataset(data_path, accum_name) as IGeoDataset;
                //IGeoDataset tDirection = OpenRasterDataset(data_path, dir_name) as IGeoDataset;
                //bodge the input extent into its nasty shell of arcobjects junk
                IRasterAnalysisEnvironment tRasterAnalysisEnvironment = new RasterAnalysisClass();
                if (analysisExtent != null)
                {
                    IRelationalOperator tCheckRequestedExtent = analysisExtent as IRelationalOperator;
                    if (tCheckRequestedExtent != null && tCheckRequestedExtent.Contains(pour_point))
                    {
                        // can anyone explain why these things have to be objects? Why can't there be interfaces
                        // for AnalysisExtentProvider and SnapObject that the datasets implement?
                        object tAnalysisEnvelopePointlesslyCastedToObject = (System.Object)analysisExtent;
                        object tAnotherPointlessMissingObject = Type.Missing;
                        //object tSnapObject = (System.Object)tDirection;
                        object tSnapObject = (System.Object)m_FlowDirDataset;
                        tRasterAnalysisEnvironment.SetExtent(esriRasterEnvSettingEnum.esriRasterEnvValue,
                            ref tAnalysisEnvelopePointlesslyCastedToObject, ref tSnapObject);
                        tRasterAnalysisEnvironment.SetAsNewDefaultEnvironment();
                    }
                    else
                    {
                        logger.LogMessage(ServerLogger.msgType.warning, "create watershed", 8000,
                            "Input point was not within requested analysis extent. Analysis extent will be ignored (may be slow)!");
                    }
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.warning, "create watershed", 8000,
                        "No analysis extent requested. Full extent will be used (may be slow)!");

                }
                IExtractionOp tExtractionOp = new RasterExtractionOpClass();

                // Do the work: snap the point to a snapped-pour-point grid and use it to calcualte the watershed
                //IGeoDataset tPourPointGrid = tExtractionOp.Points(tDirection, tPointCollection, true);
                IGeoDataset tPourPointGrid = tExtractionOp.Points(m_FlowDirDataset, tPointCollection, true);
                //IGeoDataset snapRaster = pHydrologyOp.SnapPourPoint(tPourPointGrid, tAccum, 100);
                IGeoDataset snapRaster = pHydrologyOp.SnapPourPoint(tPourPointGrid, m_FlowAccDataset, 100);
                // check the snapping worked..?
                // calculate the watershed!
                //IGeoDataset watershedRaster = pHydrologyOp.Watershed(tDirection, snapRaster);
                IGeoDataset watershedRaster = pHydrologyOp.Watershed(m_FlowDirDataset, snapRaster);
                //  restore previous default analysis extent if we changed it (should = whole dataset)
                if (analysisExtent != null)
                {
                    tRasterAnalysisEnvironment.RestoreToPreviousDefaultEnvironment();
                }
                // change it to a polygon feature (will have the area added) and return it
                IGeoDataset tWatershedPolygonGDS = ConvertAndUnionWatershed(watershedRaster);
                KeyValuePair<IGeoDataset, IGeoDataset> tRasterPolyPair = new KeyValuePair<IGeoDataset, IGeoDataset>(watershedRaster, tWatershedPolygonGDS);
                return tRasterPolyPair;
            }
            catch (Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.Message);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.ToString());
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.TargetSite.Name);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.StackTrace);
            }
            return new KeyValuePair<IGeoDataset, IGeoDataset>();
        }
        private IGeoDataset ConvertAndUnionWatershed(IGeoDataset tWatershedGDS)
        {
            //Convert the raster IGeodataset into a Polygon IFeatureClass, in a memory-workspace
            IWorkspace inMemFeatWksp = CreateInMemoryWorkspace();
            //IWorkspaceFactory pWSF = new ShapefileWorkspaceFactory();
            //IWorkspace pWS = pWSF.OpenFromFile(out_folder,0);
            string current = GetTimeStamp(DateTime.Now);
            string outname = "resultWatershed" + current;
            IFeatureClass tWaterShedPolyFC;
            IGeoDataset tInitialPolygons;
            try
            {
                IConversionOp pConversionOp = new ESRI.ArcGIS.GeoAnalyst.RasterConversionOp() as IConversionOp;
                tInitialPolygons = pConversionOp.RasterDataToPolygonFeatureData(tWatershedGDS, inMemFeatWksp, outname, false);
                tWaterShedPolyFC = tInitialPolygons as IFeatureClass;
            }
            catch 
            {
                logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                        "Error in converting watershed to in-memory FC");
                tWaterShedPolyFC = null;
                tInitialPolygons = null;
            }

            // attempt to add a CATCH_AREA field to the feature class
            bool setAreaOk = false;
            try
            {
                //setAreaOk = AddAreaField(tWaterShedPolyFC);
                setAreaOk = AddAField(tWaterShedPolyFC, "Total_Area", esriFieldType.esriFieldTypeDouble);
            }
            catch 
            {
                logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                        "Error adding area field to output");
            }

            IFeature tWaterShedFeature;
            // if there is more than one feature in the FC then union them using geometrybag
            if (tWaterShedPolyFC.FeatureCount(null) > 1)
            {
                logger.LogMessage(ServerLogger.msgType.infoStandard, "convert and union wshed", 8000,
                        "Attempting to union multiple polygons...");

                // there is more than one polygon i.e. diagonally connected. merge them into a single feature
                // with multiple rings using a geometrybag
                IGeometryBag tGeometryBag = new GeometryBagClass();
                tGeometryBag.SpatialReference = tInitialPolygons.SpatialReference;
                IFeatureCursor tFCursor = tWaterShedPolyFC.Search(null, false);
                IGeometryCollection tGeomColl = tGeometryBag as IGeometryCollection;
                IFeature tCurrentFeature = tFCursor.NextFeature();
                ITable tTable = tCurrentFeature.Table;
                while (tCurrentFeature != null)
                {
                    object missing = Type.Missing;
                    tGeomColl.AddGeometry(tCurrentFeature.Shape, ref missing, ref missing);
                    tCurrentFeature = tFCursor.NextFeature();
                }
                ITopologicalOperator tUnioned = new PolygonClass();
                tUnioned.ConstructUnion(tGeometryBag as IEnumGeometry);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "convert and union wshed", 8000,
                    "Done with ConstructUnion, doing area");
                try
                {
                    IArea tmpArea = tUnioned as IArea;
                    double tArea = tmpArea.Area;
                    // delete the previously existing rows from the table
                    tTable.DeleteSearchedRows(null);
                    // replace them with a new row representing the unioned feature
                    IRow tRow = tTable.CreateRow();
                    tRow.set_Value(tTable.FindField("SHAPE"), tUnioned);
                    tRow.set_Value(tTable.FindField("ID"), -1);
                    tRow.set_Value(tTable.FindField("GRIDCODE"), -1);
                    if (setAreaOk)
                    {
                        tRow.set_Value(tTable.FindField("Total_Area"), tArea);
                    }
                    tRow.Store();
                }
                catch (Exception ex)
                {
                    logger.LogMessage(ServerLogger.msgType.error, "store unioned polygon", 8000,
                       "Error setting fields of unioned polygon!" + ex.StackTrace + ex.Message);
                }
            }
            else
            {
                // There is only one polygon - i.e. there were not diagonally-disconnected bits
                // NB features are indexed starting at 1. Just for a laff.
                tWaterShedFeature = tWaterShedPolyFC.GetFeature(1);
                if (setAreaOk)
                {
                    try
                    {
                        int tAreaFieldIdx = tWaterShedFeature.Fields.FindField("Total_Area");
                        IArea tArea = tWaterShedFeature.Shape as IArea;
                        double tmpArea = tArea.Area;
                        tWaterShedFeature.set_Value(tAreaFieldIdx, tmpArea);
                        tWaterShedFeature.Store();
                        logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                      "Done adding area to one polygon");
                    }
                    catch
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                        "Error adding area field to single polygon output");
                    }
                }
            }
            return (IGeoDataset)tWaterShedPolyFC;
        }
        private ExtractionResultCollection ProcessExtractions(string pSearchId, IGeoDataset pInputPolygonGDS, IGeoDataset pInputRasterGDS, List<ExtractionLayerConfig> pExtractions)
        {
            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99, "Starting processing: " + pExtractions.Count.ToString() + " extractions");
            if (pExtractions.Count == 0)
            {
                return new ExtractionResultCollection(pSearchId,pInputPolygonGDS,
                    new List<RasterExtractionResult>(),new List<FeatureExtractionResult>());
            }
            // represent the polygon geodataset as an IFeatureClass for adding fields and getting the 
            // polygon feature to populate info into
            IFeatureClass tPolygonAsFC = (IFeatureClass)pInputPolygonGDS;
            IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
            IFeature tExtractionPolygonFeature = tFeatureCursor.NextFeature();
            IPolygon tExtractionPolygon = tExtractionPolygonFeature.ShapeCopy as IPolygon;
            List<RasterExtractionResult> tAllRasterResults = new List<RasterExtractionResult>();
            List<FeatureExtractionResult> tAllFeatureResults = new List<FeatureExtractionResult>();
            foreach (ExtractionLayerConfig tThisExtraction in pExtractions)
            {
                IGeoDataset tExtractionData = tThisExtraction.LayerDataset;
                switch (tThisExtraction.ExtractionType)
                {
                    case ExtractionTypes.CategoricalRaster:
                    case ExtractionTypes.ContinuousRaster:
                        RasterExtractionResult tRasterResults;
                        // use the raster mask / clipping geodataset if it's available (i.e. if this
                        // has been called from a watershed operation)
                        if (pInputRasterGDS != null)
                        {
                            tRasterResults =
                                WatershedDetailExtraction.SummariseRaster(pInputRasterGDS, tThisExtraction);
                            //WatershedDetailExtraction.SummariseCategoricalRaster(pInputRasterGDS, tCategoricalRaster);
                        }
                        // if not (this has been called from the ExtractByPolygon operation) just
                        // pass in the polygon geodataset and the clip function will handle converting to raster
                        else
                        {
                            tRasterResults =
                                WatershedDetailExtraction.SummariseRaster(pInputPolygonGDS, tThisExtraction);
                            //WatershedDetailExtraction.SummariseCategoricalRaster(pInputPolygonGDS, tCategoricalRaster);
                        }
                        tAllRasterResults.Add(tRasterResults);
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Summary done ok");
                        break;
                    case ExtractionTypes.PointFeatures:
                    case ExtractionTypes.LineFeatures:
                    case ExtractionTypes.PolygonFeatures:
                        // it is a feature class 
                        // summarise giving count features giving count and stats of the display field if it's float
                        // or count of each value if it's integer, or nothing if it's text etc
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Layer is a feature layer");
                        IFeatureClass tFC = (IFeatureClass)tExtractionData;//tDataObj as IFeatureClass;
                        FeatureExtractionResult tResult = WatershedDetailExtraction.SummariseFeatures(
                            tExtractionPolygon, tThisExtraction);
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Got extraction result - successful = " + tResult.ExtractionSuccessful.ToString());
                        tAllFeatureResults.Add(tResult);    
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Processed layer ok");
                        break;
                    // end of processing this extraction result switch statement 
                }
                // end of loop going over extraction tasks, do the next one
            }
            // all extractions (if any) are now complete and we have a list of RasterExtractionResults and 
            // a list of FeatureExtractionResults representing the results from every raster and FC extraction.
            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "All extractions done");
            
            return new ExtractionResultCollection(pSearchId, pInputPolygonGDS, tAllRasterResults, tAllFeatureResults);
        }
        #endregion
        
    
        #region Utility methods to assist with input / output
        private IFeatureClass CreateFeatureClassFromGeometry(IGeometry pGeometry, IFeatureWorkspace pOutFeatWorkspace, int wkid)
        {
            // thanks to http://bcdcspatial.blogspot.co.uk/2011/12/some-random-arcobjects-that-make.html
            // which was the only place i could find an answer to the problem I was having - the last 
            // argument to createfeatureclass is null NOT an empty string
            try
            {
                IFields tFields = new FieldsClass() as IFields;
                IFieldsEdit tFieldsEdit = (IFieldsEdit)tFields;
                IField tShpFld = new Field();
                IFieldEdit tShpEd = (IFieldEdit)tShpFld;
                tShpEd.Type_2 = esriFieldType.esriFieldTypeGeometry;
                tShpEd.Name_2 = "Shape";

                IGeometryDef tGeomDef = new GeometryDef();
                IGeometryDefEdit tGdEdit = (IGeometryDefEdit)tGeomDef;
                tGdEdit.GeometryType_2 = pGeometry.GeometryType;

                ISpatialReferenceFactory2 tSRFac = new SpatialReferenceEnvironment() as ISpatialReferenceFactory2;
                ISpatialReference tSpatRef = tSRFac.CreateSpatialReference(wkid);
                ISpatialReferenceResolution tSpatRefRes = (ISpatialReferenceResolution)tSpatRef;
                tSpatRefRes.ConstructFromHorizon();

                tGdEdit.SpatialReference_2 = tSpatRef;
                tShpEd.GeometryDef_2 = tGeomDef;
                tFieldsEdit.AddField(tShpFld);

                IObjectClassDescription tOCDesc = new FeatureClassDescription();
                for (int i = 0; i < tOCDesc.RequiredFields.FieldCount; i++)
                {
                    IField tField = tOCDesc.RequiredFields.get_Field(i);
                    if (tFieldsEdit.FindField(tField.Name) == -1)
                    {
                        tFieldsEdit.AddField(tField);
                    }
                }
                string tFCName = "tmp" + Guid.NewGuid().ToString("N");
                IFeatureClass tFC = pOutFeatWorkspace.CreateFeatureClass(
                    tFCName, tFields, null, null, esriFeatureType.esriFTSimple, "Shape", null);
                IFeature tGeomAsFeature = tFC.CreateFeature();
                tGeomAsFeature.Shape = pGeometry;
                tGeomAsFeature.Store();
                return tFC;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                logger.LogMessage(ServerLogger.msgType.error, "CreateFeatureClassFromGeometry", 99,
                              "Could not create feature class " + e.Message + e.Source + e.StackTrace);
                throw e;

            }
        }
        private IGeometry convertAnyJsonGeometry(JsonObject jsonObjectGeometry)
        {
            object[] objArray;
            //// double? nullable, 
            if (jsonObjectGeometry.TryGetArray("rings", out  objArray))
            {
                return Conversion.ToGeometry(jsonObjectGeometry, esriGeometryType.esriGeometryPolygon);
            }

            if (jsonObjectGeometry.TryGetArray("paths", out  objArray))
            {
                return Conversion.ToGeometry(jsonObjectGeometry, esriGeometryType.esriGeometryPolyline);
            }

            if (jsonObjectGeometry.TryGetArray("points", out objArray))
            {
                return Conversion.ToGeometry(jsonObjectGeometry, esriGeometryType.esriGeometryMultipoint);
            }

            try
            {
                return Conversion.ToGeometry(jsonObjectGeometry, esriGeometryType.esriGeometryPoint);
            }
            catch
            {
                try
                {
                    return Conversion.ToGeometry(jsonObjectGeometry, esriGeometryType.esriGeometryEnvelope);
                }
                catch
                {
                    logger.LogMessage(ServerLogger.msgType.debug, "process input params", 8000, "Couldn't convert geometry!");

                    return null;
                }
            }
        }

        #endregion
        #region General utility methods
        internal static bool AddAField(IFeatureClass pFeatureClass, string pFieldName, esriFieldType pFieldType, int pLength)
        {
            IField tField = new FieldClass();
            IFieldEdit tFieldEdit = tField as IFieldEdit;
            tFieldEdit.Type_2 = pFieldType;
            tFieldEdit.Name_2 = pFieldName;
            if (pFieldType == esriFieldType.esriFieldTypeString)
            {
                tFieldEdit.Length_2 = pLength;
            }
            ISchemaLock tSchemaLock = (ISchemaLock)pFeatureClass;
            bool successful = false;
            try
            {
                tSchemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
                pFeatureClass.AddField(tField);
                successful = true;
            }
            catch (Exception ex)
            {
                ServerLogger servLogger = new ServerLogger();
                servLogger.LogMessage(ServerLogger.msgType.debug, "add field to output", 8000,
                        "Couldn't get schema lock to add field " + pFieldName + " to output!" + ex.Message);
            }
            finally
            {
                tSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
            }
            //logger.LogMessage(ServerLogger.msgType.debug, "AddAField", 99,
            //                                     "Added field: " + pFieldName+", success: "+successful.ToString());
            return successful;
        }
        internal static bool AddAField(IFeatureClass pFeatureClass, string pFieldName, esriFieldType pFieldType)
        {
            bool successful = false;
            if (pFieldType == esriFieldType.esriFieldTypeString)
            {
                throw new ArgumentException("Length must be specified for a new string field", "pFieldType");
            }
            else
            {
                successful = AddAField(pFeatureClass, pFieldName, pFieldType, 0);
            }
            return successful;
        }
        private IWorkspace CreateInMemoryWorkspace()
        {
            IWorkspaceFactory workspaceFactory = new InMemoryWorkspaceFactoryClass();

            // Create an InMemory geodatabase.
            IWorkspaceName workspaceName = workspaceFactory.Create("", "MyWorkspace",
              null, 0);

            // Cast for IName.
            IName name = (IName)workspaceName;

            //Open a reference to the InMemory workspace through the name object.
            IWorkspace workspace = (IWorkspace)name.Open();
            return workspace;
        }
        private bool TryDeleteAField(IFeatureClass pFeatureClass, string pFieldName)
        {
            ISchemaLock tSchemaLock = (ISchemaLock)pFeatureClass;
            bool successful = false;
            try
            {
                tSchemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
                int tDeleteFieldNum = pFeatureClass.Fields.FindField(pFieldName);
                if (tDeleteFieldNum != -1)
                {
                    IField tDeleteField = pFeatureClass.Fields.get_Field(tDeleteFieldNum);
                    pFeatureClass.DeleteField(tDeleteField);
                    successful = true;
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.debug, "DeleteAField", 8000,
                        "Couldn't find field requested for deletion: " + pFieldName + " from feature class!");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "DeleteAField", 8000,
                        "Couldn't delete field " + pFieldName + " from feature class! " + ex.Message);
            }
            finally
            {
                tSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
            }
            return successful;
        }
        private string GetTimeStamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }
        private IEnvelope GetAnalysisEnvelope(IPoint pLocation)
        {
            IFeatureClass tHydroAreaFC = (IFeatureClass)m_ExtentFeatureDataset;
            ISpatialFilter tSpatialFilter = new SpatialFilterClass();
            tSpatialFilter.Geometry = pLocation as IGeometry;
            tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelWithin;
            tSpatialFilter.GeometryField = "SHAPE";
            IFeatureCursor tFeatureCursor = tHydroAreaFC.Search(tSpatialFilter, false);
            IFeature tHydroAreaFeature = tFeatureCursor.NextFeature(); // if there is none it will be null
            IEnvelope tHAEnvelope = tHydroAreaFeature.Extent;
            return tHAEnvelope;
        }
        #endregion
    }
    /// <summary>
    /// Object to store the configuration from an available dataset extraction (i.e. a map layer other than 
    /// the flow direction / accumulation layers). A list of these will be made corresponding to all the 
    /// available map layers during SOE Construct, and another list being a subset of that will be made when
    /// a request is received to feed to the extraction methods
    /// </summary>
    internal struct ExtractionLayerConfig
    {
        // fields: private
        private readonly int m_layerID;
        private readonly string m_layerName;
        private readonly string m_layerDescription;
        private readonly ExtractionTypes m_extractionType;
        private readonly string m_paramName;
        private readonly int m_categoryField;
        private readonly int m_valueField;
        private readonly int m_measureField;
        private readonly IGeoDataset m_LayerData;
        // properties: accessible but all read only - fields are initiated in the 
        // constructor then that's it
        internal int LayerID { get { return m_layerID; } }
        internal string LayerName { get { return m_layerName; } }
        internal string LayerDescription { get { return m_layerDescription; } }
        internal ExtractionTypes ExtractionType { get { return m_extractionType; } }
        internal string ParamName { get { return m_paramName; } }
        internal int CategoryField {get {return m_categoryField;}}
        internal int ValueField {get {return m_valueField;}}
        internal int MeasureField {get {return m_measureField;}}
        internal IGeoDataset LayerDataset { get { return m_LayerData; } }
        internal bool HasCategories {get {return m_categoryField != -1;}}
        internal bool HasValues {get {return m_valueField != -1;}}
        internal bool HasMeasures {get {
            return (m_extractionType == ExtractionTypes.LineFeatures || m_extractionType == ExtractionTypes.PolygonFeatures);
        }}

        public ExtractionLayerConfig(int id,string LayerName,string LayerDescription,
                ExtractionTypes extractiontype, string paramname,
                int CategoryFieldId,int ValueFieldId, int MeasureFieldId, IGeoDataset GeoDataset)
        {
            this.m_layerID = id;
            this.m_layerName = LayerName;
            this.m_layerDescription = LayerDescription;
            this.m_extractionType = extractiontype;
            this.m_paramName = paramname;
            this.m_categoryField = CategoryFieldId;
            this.m_valueField = ValueFieldId;
            this.m_measureField = MeasureFieldId;
            this.m_LayerData = GeoDataset;
        }
    }
    internal enum ExtractionTypes
    {
        CategoricalRaster,
        ContinuousRaster,
        PolygonFeatures,
        LineFeatures,
        PointFeatures,
        Ignore
    }
  
    /// <summary>
    /// Object to store the results of an extraction from a single raster layer
    /// </summary>
    internal struct RasterExtractionResult
    {
        private string m_paramname;
        private bool m_iscategoricalsummary;
        private Dictionary<string,double> m_resultsdictionary;
        internal string ParamName {get{return m_paramname;}}
        internal bool IsCategoricalSummary {get {return m_iscategoricalsummary;}}
        internal Dictionary<string,double> ResultsDictionary {get {return m_resultsdictionary;}}
        internal RasterExtractionResult(string paramName, bool isCategoricalSummary, Dictionary<string,double> resultsDict)
        {
            this.m_paramname = paramName;
            this.m_iscategoricalsummary = isCategoricalSummary;
            this.m_resultsdictionary = resultsDict;
        }
    }
    /// <summary>
    /// Object to store the results of all extractions (both raster and feature). Main purpose of this
    /// is to provide a means to retrieve the results for output either as attributes on the original
    /// catchment polygon (the original method) or as a structured JSON object. These abilities are implemented
    /// through the get accessors of the properties ResultsAsAttributedGeodataset and ResultsAsJson
    /// </summary>
    internal struct ExtractionResultCollection
    {
        private string m_searchId;
        private IGeoDataset m_ClippingFeature;
        private readonly List<RasterExtractionResult> m_RasterResults;
        private readonly List<FeatureExtractionResult> m_FeatureResults;
        private bool m_fieldsAdded;
        private bool m_jsonBuilt;
        private JsonObject m_ResultsJson;
        internal IGeoDataset ResultsAsAttributedGeodataset {
            get
            {
               if(!m_fieldsAdded)
               {
                    IFeatureClass tOutputAsFC = (IFeatureClass)m_ClippingFeature;
                    IFeatureCursor tFeatureCursor = tOutputAsFC.Search(null,false);
                    IFeature tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                    IPolygon tExtractionPolygon = tExtractionPolygonFeature.ShapeCopy as IPolygon;
                    #region Add results from raster extractions to output
                    foreach (RasterExtractionResult rastRes in m_RasterResults)
                    {
                        foreach (string key in rastRes.ResultsDictionary.Keys)
                        {
                            string tFieldName = rastRes.ParamName + "_" + key;
                            WatershedSOE.AddAField (tOutputAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                        }
                        tFeatureCursor = tOutputAsFC.Search(null, false);
                        tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                        foreach (KeyValuePair<string,double> tClassResult in rastRes.ResultsDictionary)
                        {
                            string tFieldName = rastRes.ParamName+ "_" + tClassResult.Key;
                            int tFieldIdx = tExtractionPolygonFeature.Fields.FindField(tFieldName);
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx,tClassResult.Value);
                                tExtractionPolygonFeature.Store();
                            }
                        }
                    }
                    #endregion
                    #region Add results from feature class extractions to output
                    foreach (FeatureExtractionResult featRes in m_FeatureResults)
                    {
                        if (featRes.ExtractionSuccessful)
                        {
                            string tFieldNameStem = featRes.ParamName + "_";
                            // first add all the field then set their values
                            WatershedSOE.AddAField(tOutputAsFC, tFieldNameStem+"Count", 
                                esriFieldType.esriFieldTypeInteger);
                            switch (featRes.ExtractionType)
                            {
                                case ExtractionTypes.PointFeatures:
                                    break;
                                case ExtractionTypes.LineFeatures:
                                    WatershedSOE.AddAField(tOutputAsFC,tFieldNameStem+"Length",
                                        esriFieldType.esriFieldTypeDouble);
                                    break;
                                case ExtractionTypes.PolygonFeatures:
                                    WatershedSOE.AddAField(tOutputAsFC,tFieldNameStem+"Area",
                                        esriFieldType.esriFieldTypeDouble);
                                    break;
                            }
                            if (featRes.HasValues)
                            {
                                WatershedSOE.AddAField(tOutputAsFC, tFieldNameStem + "Val", 
                                    esriFieldType.esriFieldTypeDouble);
                            }
                            if (featRes.HasCategories)
                            {
                                foreach (string categoryVal in featRes.CategoryCounts.Keys)
                                {
                                    WatershedSOE.AddAField(tOutputAsFC, tFieldNameStem + categoryVal + "_C",
                                        esriFieldType.esriFieldTypeInteger);
                                    if (featRes.ExtractionType == ExtractionTypes.LineFeatures){
                                        WatershedSOE.AddAField(tOutputAsFC,tFieldNameStem+categoryVal+"_Len",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                    else if (featRes.ExtractionType == ExtractionTypes.PolygonFeatures)
                                    {
                                        WatershedSOE.AddAField(tOutputAsFC, tFieldNameStem + categoryVal + "_Area",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                    
                                    if (featRes.HasValues)
                                    {
                                        WatershedSOE.AddAField(tOutputAsFC, tFieldNameStem + categoryVal + "_Val",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                }
                            }
                            // re-retrieve the catchment feature now, ensure the FC has the fields we've just added,
                            // and store the results into the feature
                            tFeatureCursor = tOutputAsFC.Search(null, false);
                            tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                            // now set the values on all the fields we've just added
                            // first the total count
                            int tFieldIdx = tOutputAsFC.FindField(tFieldNameStem+"Count");
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx,featRes.TotalCount);
                            }
                            // now the total length/area if appropriate
                            if (featRes.ExtractionType== ExtractionTypes.LineFeatures)
                            {
                                tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + "Length");
                            }
                            else if (featRes.ExtractionType == ExtractionTypes.PolygonFeatures)
                            {
                                tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + "Area");
                            }
                            else
                            {
                                tFieldIdx = -1;
                            }
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx, featRes.TotalMeasure);
                            }
                            // now the total value if appropriate
                            tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + "Val");
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx, featRes.TotalValue);
                            }
                            // now do the same for the category breakdowns
                            if (featRes.HasCategories)
                            {
                                foreach (KeyValuePair<string,int> catResult in featRes.CategoryCounts)
                                {
                                    // set a category count field for all shape types
                                    tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + catResult.Key + "_C");
                                    if (tFieldIdx != -1)
                                    {
                                        tExtractionPolygonFeature.set_Value(tFieldIdx, catResult.Value);
                                    }
                                    // set a category measure field for polyline / polygon fields only
                                    if (featRes.ExtractionType== ExtractionTypes.LineFeatures)
                                    {
                                        tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + catResult.Key + "_Len");
                                    }
                                    else if (featRes.ExtractionType == ExtractionTypes.PolygonFeatures)
                                    {
                                        tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + catResult.Key + "_Area"); 
                                    }
                                    else { tFieldIdx = -1; }
                                    if (tFieldIdx != -1)
                                    {
                                        // CategoryMeasures and CategoryCounts have identical keys
                                        if(featRes.CategoryMeasures.ContainsKey(catResult.Key))
                                        {
                                            tExtractionPolygonFeature.set_Value(tFieldIdx, featRes.CategoryMeasures[catResult.Key]);
                                        }
                                        else
                                        {
                                          //  logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                            //     "Whoops! CategoryMeasures dictionary doesn't contain category result key "+catResult.Key);
                                        }
                                    }
                                    if (featRes.HasValues)
                                    {
                                        tFieldIdx = tOutputAsFC.FindField(tFieldNameStem + catResult.Key + "_Val");
                                        if (tFieldIdx != -1)
                                        {
                                            // CategoryTotals and CategoryCounts also have identical keys
                                            if (featRes.CategoryMeasures.ContainsKey(catResult.Key))
                                            {
                                                tExtractionPolygonFeature.set_Value(tFieldIdx, featRes.CategoryTotals[catResult.Key]);
                                            }
                                            else
                                            {
                                                //logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                // "Whoops! CategoryTotals dictionary doesn't contain category result key " + catResult.Key);
                                            }
                                        }
                                    }
                                //next category
                                }
                            //done all categories
                            }
                            // all values set
                            tExtractionPolygonFeature.Store();
                        }
                    }
                    #endregion
                    m_fieldsAdded = true;
               }
               return m_ClippingFeature; 
            }
        }
        internal JsonObject ResultsAsJson {
            get
            {
                if (!m_jsonBuilt)
                {
                    JsonObject tResults = new JsonObject();
                    IFeatureClass tPolygonAsFC = (IFeatureClass)m_ClippingFeature;
                    IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                    IFeature tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                    IGeometry tExtractionPolygon = tExtractionPolygonFeature.ShapeCopy as IGeometry;
                    JsonObject tPolygonAsJson = Conversion.ToJsonObject(tExtractionPolygon);
                    tResults.AddString("search_id", m_searchId);
                    double tArea = (double)tExtractionPolygonFeature.get_Value(tExtractionPolygonFeature.Fields.FindField("Total_Area"));
                    tResults.AddDouble("total_area", tArea);
                    tResults.AddJsonObject("geometry", tPolygonAsJson);
                    tResults.AddLong("output_wkid",m_ClippingFeature.SpatialReference.FactoryCode);
                    JsonObject tExtractionsJson = new JsonObject();
                    #region Add results from raster extractions to output

                    foreach (RasterExtractionResult rastRes in m_RasterResults)
                    {
                        JsonObject tRastJson = new JsonObject();
                        tRastJson.AddString("Param", rastRes.ParamName);
                        tRastJson.AddString("ExtractionType",rastRes.IsCategoricalSummary?
                            ExtractionTypes.CategoricalRaster.ToString() : ExtractionTypes.ContinuousRaster.ToString());
                        //tRastJson.AddBoolean("extractiontype", rastRes.IsCategoricalSummary);
                        JsonObject tResJson = new JsonObject();
                        foreach (KeyValuePair<string, double> kvp in rastRes.ResultsDictionary)
                        {
                            tResJson.AddDouble(kvp.Key, kvp.Value);
                        }
                        tRastJson.AddJsonObject("Results", tResJson);
                        tExtractionsJson.AddJsonObject(rastRes.ParamName, tRastJson);
                    }
                    #endregion
                    #region Add results from feature extractions to output
                    foreach (FeatureExtractionResult featRes in m_FeatureResults)
                    {
                        JsonObject tFeatJson = new JsonObject();
                        tFeatJson.AddString("Param", featRes.ParamName);
                        tFeatJson.AddString("ExtractionType", featRes.ExtractionType.ToString());
                        JsonObject tResJson = new JsonObject();
                        tResJson.AddLong("Count", featRes.TotalCount);
                        if (featRes.ExtractionType == ExtractionTypes.LineFeatures)
                        {
                            tResJson.AddDouble("Length", featRes.TotalMeasure);
                        }
                        else if (featRes.ExtractionType == ExtractionTypes.PolygonFeatures)
                        {
                            tResJson.AddDouble("Area", featRes.TotalMeasure);
                        }
                        if (featRes.HasValues)
                        {
                            tResJson.AddDouble("Value", featRes.TotalValue);
                        }
                        if (featRes.HasCategories)
                        {
                            JsonObject tCategoriesJson = new JsonObject();
                            foreach (string categoryVal in featRes.CategoryCounts.Keys)
                            {
                                JsonObject tCategoryJson = new JsonObject();
                                tCategoryJson.AddLong("Count", featRes.CategoryCounts[categoryVal]);
                                if (featRes.ExtractionType == ExtractionTypes.LineFeatures)
                                {
                                    tCategoryJson.AddDouble("Length", featRes.CategoryMeasures[categoryVal]);
                                }
                                else if (featRes.ExtractionType == ExtractionTypes.PolygonFeatures)
                                {
                                    tCategoryJson.AddDouble("Area", featRes.CategoryMeasures[categoryVal]);
                                }
                                if (featRes.HasValues)
                                {
                                    tCategoryJson.AddDouble("Value", featRes.CategoryTotals[categoryVal]);
                                }
                                tCategoriesJson.AddJsonObject(categoryVal, tCategoryJson);
                            }
                            tResJson.AddJsonObject("Categories", tCategoriesJson);
                        }
                        tFeatJson.AddJsonObject("Results",tResJson);
                        tExtractionsJson.AddJsonObject(featRes.ParamName, tFeatJson);
                    }
                    tResults.AddJsonObject("Extractions",tExtractionsJson);
                    #endregion
                    m_ResultsJson = tResults;
                    m_jsonBuilt = true;
                }
                return m_ResultsJson;
            }
        }

        internal ExtractionResultCollection(string SearchId, IGeoDataset ClippingFeature,
            List<RasterExtractionResult> RasterResults,
            List<FeatureExtractionResult> FeatureResults
        )
        {
            this.m_searchId = SearchId;
            this.m_ClippingFeature = ClippingFeature;
            this.m_RasterResults = RasterResults;
            this.m_FeatureResults = FeatureResults;
            this.m_fieldsAdded = false;
            this.m_jsonBuilt = false;
            this.m_ResultsJson = new JsonObject();
        }
    }
}
            