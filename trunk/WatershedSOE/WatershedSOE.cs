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

        //private string data_path = "C:\\arcgisserver\\SOE\\WatershedSOE\\WatershedData.gdb";
        private string data_path = "C:\\arcgisserver\\SOE\\WatershedSOE\\gridformat";
        private string out_folder = "C:\\arcgisserver\\SOE\\WatershedSOE\\";
        private string irn_path = "C:\\arcgisserver\\data\\IRN";
        private string irn_vector_path = "C:\\arcgisserver\\data\\IRN\\network_analyst_test.gdb";
        private string lcm2kname = "lcm_50m";
        private string elevname = "irn_elev";
        private string riversname = "irn_import";

        private string accum_name = "fac";
        private string dir_name = "fdr";
        
        // variables to do with configuring the data sources automatically
        private string m_FlowAccLayerName;
        private string m_FlowDirLayerName;
        private string m_ExtentFeatureLayerName;
        // variables to hold list of layer ids for each extraction type
        private List<ExtractionLayerConfig> m_ExtractableParams = new List<ExtractionLayerConfig>();
        private IGeoDataset m_FlowDirDataset;
        private IGeoDataset m_FlowAccDataset;
        private IGeoDataset m_ExtentFeatureDataset;
        // true if both flow acc and flow dir layers are found
        // otherwise can only extract by input polygon (watershed operation not added to REST schema)
        private bool m_CanDoWatershed;

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
                if (props.GetProperty("FlowAccLayer") != null)
                {
                    m_FlowAccLayerName = props.GetProperty("FlowAccLayer") as string;
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Flow Accumulation layer: "+m_FlowAccLayerName);

                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: FlowAccum property missing");
                    throw new ArgumentNullException();
                }
                if (props.GetProperty("FlowDirLayer") != null)
                {
                    m_FlowDirLayerName = props.GetProperty("FlowDirLayer") as string;
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Flow Direction layer: "+m_FlowDirLayerName);
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flowdir property missing");
                    throw new ArgumentNullException();
                }
                if (props.GetProperty("ExtentFeatureLayer") != null)
                {
                    m_ExtentFeatureLayerName = props.GetProperty("ExtentFeatureLayer") as string;
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: found definition for Extent Feature layer: " + m_ExtentFeatureLayerName);
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: no definition for extent feature layers found. Extent may still be passed as input");
                    // no exception, as this isn't a required layer
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
                // parameter
                // We only need to do this at startup not each time
                IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
                string mapName = mapServer.DefaultMapName;
                IMapLayerInfo layerInfo;
                IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
                IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
                
                int c = layerInfos.Count;
                int acc_layerIndex=0;
                int dir_layerIndex=0;
                int ext_layerIndex=0;
                //Dictionary<int,string> other_layerIndices = new Dictionary<int,string>();
                for (int i=0;i<c;i++)
                {
                    layerInfo = layerInfos.get_Element(i);
                    if(layerInfo.Name == m_FlowAccLayerName)
                    {
                        acc_layerIndex = i;
                    }
                    else if (layerInfo.Name == m_FlowDirLayerName)
                    {
                        dir_layerIndex = i;
                    }
                    else if (m_ExtentFeatureLayerName != null && layerInfo.Name == m_ExtentFeatureLayerName)
                    {
                        ext_layerIndex = i;
                    }
                    else
                    {
                        // Types appear to be "Raster Layer", "Feature Layer", and "Group Layer"
                        logger.LogMessage(ServerLogger.msgType.infoStandard, 
                            "Construct", 8000, 
                            "WSH: found additional map layer "+layerInfo.Name+" at ID "+
                            layerInfo.ID+" of type "+layerInfo.Type+" with display field "+layerInfo.DisplayField);
                        if (layerInfo.Type == "Raster Layer" || layerInfo.Type == "Feature Layer")
                        {
                            string tName = layerInfo.Name;
                            string tParamName = tName.Split(':')[0];
                            if (tParamName.Length > 6)
                            {
                                tParamName = layerInfo.Description.Split(':')[0];
                            }
                            if (tParamName.Length > 6)
                            {
                                // fail if any of the map layers except the ones used for the catchment definition
                                // don't have a name or description starting with 6 or less characters followed by :
                                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000,
                                     " Watershed SOE warning: could determine output parameter string for layer " + tName +
                                     " and it will not be available for extraction. "+
                                     " Ensure that either the layer name or description starts with an ID for the " +
                                     " service parameter name to be exposed, max 6 characters and separated by ':'" +
                                     " e.g. 'LCM2K:Land Cover Map 2000'");
                                continue;
                                //throw new ArgumentException();
                            }
                            ExtractionTypes tExtractionType = ExtractionTypes.Ignore;
                            if (layerInfo.Type == "Raster Layer")
                            {
                                IRaster tRaster = dataAccess.GetDataSource(mapName, i) as IRaster;
                                IRasterProps tRasterProps = tRaster as IRasterProps;
                                if (tRasterProps.IsInteger)
                                {
                                    tExtractionType = ExtractionTypes.CategoricalRaster;
                                }
                                else
                                {
                                    tExtractionType = ExtractionTypes.ContinuousRaster;
                                }
                                ExtractionLayerConfig tLayerInfo = new ExtractionLayerConfig
                                    (i, tExtractionType, tParamName,-1,-1,-1);
                                m_ExtractableParams.Add(tLayerInfo);
                            }
                            else
                            {
                                IFeatureClass tFC = dataAccess.GetDataSource(mapName, i) as IFeatureClass;
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
                                int tValueField= layerInfo.Fields.FindFieldByAliasName("VALUE");
                                int tMeasureField = layerInfo.Fields.FindFieldByAliasName("MEASURE");
                                ExtractionLayerConfig tLayerInfo = new ExtractionLayerConfig
                                    (i, tExtractionType, tParamName,tCategoryField,tValueField,tMeasureField);
                                m_ExtractableParams.Add(tLayerInfo);
                                // layers with any other geometry type will be ignored
                            }
                        }
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
            catch
            {
                logger.LogMessage(ServerLogger.msgType.error,"Construct",8000,"Watershed SOE error: could not get the datasets associated with configured map layers");
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
            IMapLayerInfo layerInfo;
            IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
            List<string> tOptionalParams = new List<string>();
            foreach (ExtractionLayerConfig tExtractableLayer in m_ExtractableParams)
            {
                tOptionalParams.Add(tExtractableLayer.ParamName);
                    logger.LogMessage(ServerLogger.msgType.infoDetailed, "CreateRestSchema", 8000,
                        "WSH: added parameter "+tExtractableLayer.ParamName);
            }
            List<string> tPermanentParams = new List<string>(){"hydroshed_id","location","extent"};
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
            List<string> tPolygonExtractionParams = new List<string>(){"extraction_id","polygon"};
            tPolygonExtractionParams.AddRange(tOptionalParams);
            RestOperation extractByPolygonOper = new RestOperation("extractByPolygon",
                                                    tPolygonExtractionParams.ToArray(),
                                                    new string[] { "json" },
                                                    ExtractByPolygonHandler);
            rootRes.operations.Add(extractByPolygonOper);
            return rootRes;
        }

        private byte[] RootResHandler(NameValueCollection boundVariables, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;
            JsonObject result = new JsonObject();
            return Encoding.UTF8.GetBytes(result.ToJson());
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
            string hydroshed_id;
            bool found = operationInput.TryGetString("hydroshed_id", out hydroshed_id);
            if (!found || string.IsNullOrEmpty(hydroshed_id))
            {
                throw new ArgumentNullException("hydroshed_id");
            }
            // input point - REQUIRED - the search location
            JsonObject jsonPoint;
            found = operationInput.TryGetJsonObject("location", out jsonPoint);
            if (!found)
            {
                throw new ArgumentNullException("location");
            }
            IPoint locationpoint = Conversion.ToGeometry(jsonPoint, esriGeometryType.esriGeometryPoint) as IPoint;
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
            #endregion

            #region Optional parameters - catchment characteristics to extract
            //LCM2000 (categorical raster)
            bool? nullableBool;
            bool doLCM2000;
            found = operationInput.TryGetAsBoolean("lcm2k", out nullableBool);
            if ((!nullableBool.HasValue) || (!found)) { doLCM2000 = false; }
            else { doLCM2000 = (bool)nullableBool; }
            // Elevation (continuous raster)
            bool doElevation;
            found = operationInput.TryGetAsBoolean("elev", out nullableBool);
            if ((!nullableBool.HasValue) || (!found)) { doElevation = false; }
            else { doElevation = (bool)nullableBool; }
            // Total upstream length (summary of vector features)
            bool doTotalUpstream;
            found = operationInput.TryGetAsBoolean("totalupstream", out nullableBool);
            if ((!nullableBool.HasValue) || (!found)) { doTotalUpstream = false; }
            else { doTotalUpstream = (bool)nullableBool; }
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
            //IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
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
                bool addedSearchId = AddAField(tPolygonAsFC, "SEARCH_ID", esriFieldType.esriFieldTypeString, hydroshed_id.Length);
                bool addedOutletX = AddAField(tPolygonAsFC, "OUTLET_X", esriFieldType.esriFieldTypeDouble);
                bool addedOutletY = AddAField(tPolygonAsFC, "OUTLET_Y", esriFieldType.esriFieldTypeDouble);
                IFeature tCatchmentFeature;
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                tCatchmentFeature = tFeatureCursor.NextFeature(); // there will only be one, not bothering with loop
                if (addedSearchId)
                {
                    try
                    {
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("SEARCH_ID"), hydroshed_id);
                        tCatchmentFeature.Store();
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "error setting search id field with value " +
                            hydroshed_id + ". Detail: " + ex.StackTrace + " " + ex.Message);
                    }
                }
                if (addedOutletX)
                {
                    try
                    {
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("OUTLET_X"), locationpoint.X);
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
                        tCatchmentFeature.set_Value(tCatchmentFeature.Fields.FindField("OUTLET_Y"), locationpoint.Y);
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
            /*if (doLCM2000)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "attempting lcm2k extraction");
                // Get the polygon - can't use tPolygonAsFC.GetFeature(1), reason as above
                IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
                IGeoDataset tLCM2KGDS = OpenRasterDataset(irn_path, lcm2kname) as IGeoDataset;
                Dictionary<int, double> tLCMResults = WatershedDetailExtraction.
                        SummariseCategoricalRaster(tWatershedRasterGDS, tLCM2KGDS);
                foreach (int key in tLCMResults.Keys)
                {
                    string tFieldName = "LCM2K_" + key.ToString();
                    AddAField(tPolygonAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                }
                // retrieve the catchment feature now to ensure it's got the fields we've just added
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                IFeature tCatchmentFeature = tFeatureCursor.NextFeature();
                foreach (KeyValuePair<int, double> tClassResult in tLCMResults)
                {
                    string tFieldName = "LCM2K_" + tClassResult.Key.ToString();
                    int tFieldIdx = tCatchmentFeature.Fields.FindField(tFieldName);
                    if (tFieldIdx != -1)
                    {
                        tCatchmentFeature.set_Value(tFieldIdx, tClassResult.Value);
                        tCatchmentFeature.Store();
                    }
                }
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "LCM2k extraction ran ok");
            }
            if (doElevation)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "Attempting elev extraction");
                // can't use tPolygonAsFC.GetFeature(1), reason as above
                IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
                IGeoDataset tElevGDS = OpenRasterDataset(irn_path, elevname) as IGeoDataset;
                Dictionary<string, double> tElevResults =
                    WatershedDetailExtraction.SummariseContinuousRaster(tWatershedRasterGDS, tElevGDS);
                foreach (string key in tElevResults.Keys)
                {
                    string tFieldName = "ELEV_" + key.ToString();
                    AddAField(tPolygonAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                }
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                IFeature tCatchmentFeature = tFeatureCursor.NextFeature();
                foreach (KeyValuePair<string, double> tClassResult in tElevResults)
                {
                    string tFieldName = "ELEV_" + tClassResult.Key.ToString();
                    int tFieldIdx = tCatchmentFeature.Fields.FindField(tFieldName);
                    if (tFieldIdx != -1)
                    {
                        tCatchmentFeature.set_Value(tFieldIdx, tClassResult.Value);
                        tCatchmentFeature.Store();
                    }
                }
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "Elev extraction ran ok");
            }*/

            /*if (doTotalUpstream)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "Attempting total upstream summary");
                string tFieldName = "SUM_UPSTRM";
                IFeatureClass tPolygonAsFC = (IFeatureClass)tWatershedPolyGDS;
                AddAField(tPolygonAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                // can't use tPolygonAsFC.GetFeature(1), reason as above
                IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null, false);
                IFeature tCatchmentFeature = tFeatureCursor.NextFeature();
                IPolygon tCatchmentPolygon = (IPolygon)tCatchmentFeature.ShapeCopy;
                IGeoDataset tRiversGDS = OpenVectorDataset(irn_vector_path, riversname) as IGeoDataset;
                if (tRiversGDS != null)
                {
                    double tTotalUpstream = WatershedDetailExtraction.FindTotalUpstreamLength(tCatchmentPolygon, tRiversGDS);
                    int tFieldIdx = tCatchmentFeature.Fields.FindField(tFieldName);
                    if (tFieldIdx != -1)
                    {
                        tCatchmentFeature.set_Value(tFieldIdx, tTotalUpstream);
                        tCatchmentFeature.Store();
                    }
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.debug, "create watershed handler", 99, "rivers GDS is null...");
                }
            }*/
            // Use the catchment polygon as input into the generic extraction method
            IGeoDataset tFinalPolygonGDS = ProcessExtractions(tWatershedPolyGDS, tWatershedRasterGDS, extractionRequests);
            #endregion
            // The catchment feature now exists and has all attributes requested. Ready to go.
            // TODO - implement alternative return formats. Either do as follows OR if user chooses not to 
            // return geometry (e.g. for mobile app), build a JSON object manually that has LCM classes etc
            // as nested objects (will make it easier for client to handle LCM, Elevation ,etc, separately)
            IRecordSetInit returnRecSet = new RecordSetClass();
            //returnRecSet.SetSourceTable(tWatershedPolyGDS as ITable, null);
            returnRecSet.SetSourceTable(tFinalPolygonGDS as ITable, null);
            IRecordSet recset = returnRecSet as IRecordSet;
            byte[] jsonFeatures = Conversion.ToJson(recset);
            return jsonFeatures;
        }
        private IGeoDataset ProcessExtractions(IGeoDataset pInputPolygonGDS, IGeoDataset pInputRasterGDS, List<ExtractionLayerConfig> pExtractions)
        {
            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99, "Starting processing: "+pExtractions.Count.ToString()+" extractions");
            if (pExtractions.Count == 0)
            {
                return pInputPolygonGDS;
            }
            // stuff required to retrieve data from map layers
            IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
            string mapName = mapServer.DefaultMapName;
            IMapLayerInfo layerInfo;
            IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
            IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
            // represent the polygon geodataset as an IFeatureClass for adding fields and getting the 
            // polygon feature to populate info into
            IFeatureClass tPolygonAsFC = (IFeatureClass)pInputPolygonGDS;
            IFeatureCursor tFeatureCursor = tPolygonAsFC.Search(null,false);
            IFeature tExtractionPolygonFeature = tFeatureCursor.NextFeature();
            IPolygon tExtractionPolygon = tExtractionPolygonFeature.ShapeCopy as IPolygon;

            foreach (ExtractionLayerConfig tThisExtraction in pExtractions)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99, 
                       "Fetching source of layer "+tThisExtraction.ParamName+" in layer id "+tThisExtraction.LayerID.ToString());
                object tDataObj = dataAccess.GetDataSource(mapName, tThisExtraction.LayerID);
                switch (tThisExtraction.ExtractionType)
                {
                    case ExtractionTypes.CategoricalRaster:
                        // summarise categorical raster giving a count of pixels of each value
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99, 
                            "Layer is a categorical raster");
                        IGeoDataset tCategoricalRaster = tDataObj as IGeoDataset;
                        Dictionary<int, double> tCategoricalResults = 
                            WatershedDetailExtraction.SummariseCategoricalRaster(pInputRasterGDS, tCategoricalRaster);
                        foreach (int key in tCategoricalResults.Keys)
                        {
                            string tFieldName = tThisExtraction.ParamName + "_" + key.ToString();
                            AddAField(tPolygonAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                        }
                        // re-retrieve the catchment feature now, ensure the FC has the fields we've just added,
                        // and store the results into the feature
                        tFeatureCursor = tPolygonAsFC.Search(null, false);
                        tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                        foreach (KeyValuePair<int,double> tClassResult in tCategoricalResults)
                        {
                            string tFieldName = tThisExtraction.ParamName+ "_" + tClassResult.Key.ToString();
                            int tFieldIdx = tExtractionPolygonFeature.Fields.FindField(tFieldName);
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx,tClassResult.Value);
                                tExtractionPolygonFeature.Store();
                            }
                        }
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Summary done ok");
                        break;
                    case ExtractionTypes.ContinuousRaster:
                        // summarise continuous raster giving stats of the range of values
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Layer is a continuous raster");
                        IGeoDataset tContinuousRaster = tDataObj as IGeoDataset;
                        Dictionary<string, double> tContinuousResults =
                            WatershedDetailExtraction.SummariseContinuousRaster(pInputRasterGDS, tContinuousRaster);
                        foreach (string key in tContinuousResults.Keys)
                        {
                            string tFieldName = tThisExtraction.ParamName + "_" + key.ToString();
                            AddAField(tPolygonAsFC, tFieldName, esriFieldType.esriFieldTypeDouble);
                        }
                        // re-retrieve the catchment feature now, ensure the FC has the fields we've just added,
                        // and store the results into the feature
                        tFeatureCursor = tPolygonAsFC.Search(null, false);
                        tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                        foreach (KeyValuePair<string,double> tClassResult in tContinuousResults)
                        {
                            string tFieldName = tThisExtraction.ParamName+ "_" + tClassResult.Key.ToString();
                            //int tFieldIdx = tCatchmentFeature.Fields.FindField(tFieldName);
                            int tFieldIdx = tExtractionPolygonFeature.Fields.FindField(tFieldName);
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx,tClassResult.Value);
                                tExtractionPolygonFeature.Store();
                            }
                        }
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Summary done ok");
                        break;
                    default:
                        // it is a feature class 
                        // summarise giving count features giving count and stats of the display field if it's float
                        // or count of each value if it's integer, or nothing if it's text etc
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Layer is a feature layer");
                        IFeatureClass tFC = tDataObj as IFeatureClass;
                        if (tFC == null)
                        {
                            continue;
                        }
                        FeatureExtractionResult tResult = WatershedDetailExtraction.SummariseFeatures(
                            tExtractionPolygon, tFC, tThisExtraction.CategoryField,tThisExtraction.ValueField,
                            tThisExtraction.MeasureField);
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Got extraction result - successful = "+tResult.ExtractionSuccessful.ToString());
                        
                        if (tResult.ExtractionSuccessful)
                        {
                            string tFieldNameStem = tThisExtraction.ParamName + "_";
                            // first add all the field then set their values
                            AddAField(tPolygonAsFC, tFieldNameStem+"Count", esriFieldType.esriFieldTypeInteger);
                            switch (tFC.ShapeType)
                            {
                                case esriGeometryType.esriGeometryPoint:
                                    break;
                                case esriGeometryType.esriGeometryPolyline:
                                    AddAField(tPolygonAsFC,tFieldNameStem+"Length",esriFieldType.esriFieldTypeDouble);
                                    break;
                                case esriGeometryType.esriGeometryPolygon:
                                    AddAField(tPolygonAsFC,tFieldNameStem+"Area",esriFieldType.esriFieldTypeDouble);
                                    break;
                            }
                            if (tResult.HasValues)
                            {
                                AddAField(tPolygonAsFC, tFieldNameStem + "Val", esriFieldType.esriFieldTypeDouble);
                            }
                            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                 "Has values: "+tResult.HasValues.ToString());
                            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                 "Has categories: " + tResult.HasCategories.ToString());

                            if (tResult.HasCategories)
                            {
                                foreach (string categoryVal in tResult.CategoryCounts.Keys)
                                {
                                    AddAField(tPolygonAsFC, tFieldNameStem + categoryVal + "_C",
                                        esriFieldType.esriFieldTypeInteger);
                                    if (tFC.ShapeType == esriGeometryType.esriGeometryPolyline){
                                        AddAField(tPolygonAsFC,tFieldNameStem+categoryVal+"_Len",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                    else if (tFC.ShapeType == esriGeometryType.esriGeometryPolygon)
                                    {
                                        AddAField(tPolygonAsFC, tFieldNameStem + categoryVal + "_Area",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                    
                                    if (tResult.HasValues)
                                    {
                                        AddAField(tPolygonAsFC, tFieldNameStem + categoryVal + "_Val",
                                            esriFieldType.esriFieldTypeDouble);
                                    }
                                }
                            }
                            // re-retrieve the catchment feature now, ensure the FC has the fields we've just added,
                            // and store the results into the feature
                            tFeatureCursor = tPolygonAsFC.Search(null, false);
                            tExtractionPolygonFeature = tFeatureCursor.NextFeature();
                            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                 "Added required fields... ");

                            // now set the values on all the fields we've just added
                            // first the total count
                            int tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem+"Count");
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx,tResult.TotalCount);
                            }
                            // now the total length/area if appropriate
                            if (tFC.ShapeType == esriGeometryType.esriGeometryPolyline)
                            {
                                tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + "Length");
                            }
                            else if (tFC.ShapeType == esriGeometryType.esriGeometryPolygon)
                            {
                                tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + "Area");
                            }
                            else
                            {
                                tFieldIdx = -1;
                            }
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx, tResult.TotalMeasure);
                            }
                            // now the total value if appropriate
                            tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + "Val");
                            if (tFieldIdx != -1)
                            {
                                tExtractionPolygonFeature.set_Value(tFieldIdx, tResult.TotalValue);
                            }
                            // now do the same for the category breakdowns
                            if (tResult.HasCategories)
                            {
                                foreach (KeyValuePair<string,int> catResult in tResult.CategoryCounts)
                                {
                                    // set a category count field for all shape types
                                    tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + catResult.Key + "_C");
                                    if (tFieldIdx != -1)
                                    {
                                        tExtractionPolygonFeature.set_Value(tFieldIdx, catResult.Value);
                                    }
                                    // set a category measure field for polyline / polygon fields only
                                    if (tFC.ShapeType == esriGeometryType.esriGeometryPolyline)
                                    {
                                        tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + catResult.Key + "_Len");
                                    }
                                    else if (tFC.ShapeType == esriGeometryType.esriGeometryPolygon)
                                    {
                                        tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + catResult.Key + "_Area"); 
                                    }
                                    else { tFieldIdx = -1; }
                                    if (tFieldIdx != -1)
                                    {
                                        // CategoryMeasures and CategoryCounts have identical keys
                                        if(tResult.CategoryMeasures.ContainsKey(catResult.Key))
                                        {
                                            tExtractionPolygonFeature.set_Value(tFieldIdx, tResult.CategoryMeasures[catResult.Key]);
                                        }
                                        else
                                        {
                                            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                 "Whoops! CategoryMeasures dictionary doesn't contain category result key "+catResult.Key);
                                        }
                                    }
                                    if (tResult.HasValues)
                                    {
                                        tFieldIdx = tPolygonAsFC.FindField(tFieldNameStem + catResult.Key + "_Val");
                                        if (tFieldIdx != -1)
                                        {
                                            // CategoryTotals and CategoryCounts also have identical keys
                                            if (tResult.CategoryMeasures.ContainsKey(catResult.Key))
                                            {
                                                tExtractionPolygonFeature.set_Value(tFieldIdx, tResult.CategoryTotals[catResult.Key]);
                                            }
                                            else
                                            {
                                                logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                                                 "Whoops! CategoryTotals dictionary doesn't contain category result key " + catResult.Key);
                                            }
                                        }
                                    }
                                //next category
                                }
                            //done all categories
                            }
                            // all values set
                            tExtractionPolygonFeature.Store();
                        // end of processing results from this particular feature class and saving into output polygon
                        }
                        logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "Processed layer ok");
                        break;
                // end of processing this extraction result switch statement 
                }
            // end of loop going over extraction tasks, do the next one
            }
            // all extractions (if any) are now complete and the output polygon contains fields representing the results
            // from every raster and FC extraction
            // remember everything's by reference so with all that feature class business we have just modified
            // the input: return ready for output
            logger.LogMessage(ServerLogger.msgType.debug, "ProcessExtractions", 99,
                            "All extractions done");
                        
            return pInputPolygonGDS;
        }
        private byte[] ExtractByPolygonHandler(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            // TODO implement code for extraction by input polygon
            #region Process the REST arguments
            // hydroshed_id - REQUIRED - to identify the overall result
            string extraction_id;
            bool found = operationInput.TryGetString("extraction_id", out extraction_id);
            if (!found || string.IsNullOrEmpty(extraction_id))
            {
                throw new ArgumentNullException("extraction_id");
            }
            // input polygon - REQUIRED - the polygon to summarise data within
            JsonObject jsonPolygon;
            found = operationInput.TryGetJsonObject("polygon", out jsonPolygon);
            if (!found)
            {
                throw new ArgumentNullException("polygon");
            }
            IPolygon extractionPolygon = Conversion.ToGeometry(jsonPolygon, esriGeometryType.esriGeometryPolygon) as IPolygon;
            
            responseProperties = null;
            return null;
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



        private KeyValuePair<IGeoDataset,IGeoDataset> computeWatershed(IPoint pour_point, IEnvelope analysisExtent)
        {
            try
            {
                //bodge the input point into its nasty shell of arcobjects junk for analysis
                IHydrologyOp pHydrologyOp = new RasterHydrologyOp() as IHydrologyOp;
                IPointCollection3 tPointCollection = new MultipointClass();
                object tPointlessMissingObject = Type.Missing;
                tPointCollection.AddPoint(pour_point, ref tPointlessMissingObject, ref tPointlessMissingObject);

                // open the accumulation and direction datasets, hardcoded
                IGeoDataset tAccum = OpenRasterDataset(data_path, accum_name) as IGeoDataset;
                IGeoDataset tDirection = OpenRasterDataset(data_path, dir_name) as IGeoDataset;
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
                        object tSnapObject = (System.Object)tDirection;
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
                IGeoDataset tPourPointGrid = tExtractionOp.Points(tDirection, tPointCollection, true);
                IGeoDataset snapRaster = pHydrologyOp.SnapPourPoint(tPourPointGrid, tAccum, 100);
                // check the snapping worked..?
                // calculate the watershed!
                IGeoDataset watershedRaster = pHydrologyOp.Watershed(tDirection, snapRaster);
               
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
            catch(Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.Message);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.ToString());
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.TargetSite.Name);
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Compute watershed error: ", 8000, e.StackTrace);
            }
            return new KeyValuePair<IGeoDataset, IGeoDataset>();
        }
        
        private IGeoDataset ConvertAndUnionWatershed(IGeoDataset tWatershedGDS){
            //Convert the raster IGeodataset into a Polygon IFeatureClass, in a memory-workspace
            IWorkspace inMemFeatWksp = CreateInMemoryWorkspace();
            //IWorkspaceFactory pWSF = new ShapefileWorkspaceFactory();
            //IWorkspace pWS = pWSF.OpenFromFile(out_folder,0);
            string current = GetTimeStamp(DateTime.Now);
            string outname = "resultWatershed"+current;
            IFeatureClass tWaterShedPolyFC;
            IGeoDataset tInitialPolygons;
            try
            {
                IConversionOp pConversionOp = new ESRI.ArcGIS.GeoAnalyst.RasterConversionOp() as IConversionOp;
                tInitialPolygons = pConversionOp.RasterDataToPolygonFeatureData(tWatershedGDS, inMemFeatWksp, outname, false);
                tWaterShedPolyFC = tInitialPolygons as IFeatureClass;
            }
            catch (Exception e)
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
                setAreaOk = AddAreaField(tWaterShedPolyFC);
            }
            catch (Exception e)
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
                        tRow.set_Value(tTable.FindField("CATCH_AREA"), tArea);
                    }
                    tRow.Store();
                }
                catch (Exception ex)
                {
                    logger.LogMessage(ServerLogger.msgType.error, "store unioned polygon", 8000,
                       "Error setting fields of unioned polygon!" + ex.StackTrace+ex.Message);
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
                        int tAreaFieldIdx = tWaterShedFeature.Fields.FindField("CATCH_AREA");
                        IArea tArea = tWaterShedFeature.Shape as IArea;
                        double tmpArea = tArea.Area;
                        tWaterShedFeature.set_Value(tAreaFieldIdx, tmpArea);
                        tWaterShedFeature.Store();
                        logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                      "Done adding area to one polygon");
                    }
                    catch (Exception e)
                    {
                        logger.LogMessage(ServerLogger.msgType.debug, "convert and union wshed", 8000,
                        "Error adding area field to single polygon output");
                    }
                }
            }
            return (IGeoDataset)tWaterShedPolyFC;
        }

        private bool AddAreaField(IFeatureClass fc)
        {
            IField tField = new FieldClass();
            IFieldEdit tFieldEdit = tField as IFieldEdit;
            tFieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            tFieldEdit.Name_2 = "CATCH_AREA";
            ISchemaLock schemaLock = (ISchemaLock)fc;
            bool successful = false;
            try
            {
                schemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
                fc.AddField(tField);
                successful = true;
            }
            catch (Exception ex)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "calculate area", 8000,
                        "Couldn't get schema lock to add area field to output!"+ex.Message);
            }
            finally
            {
                schemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
            }
            return successful;
        }
        private bool AddAField(IFeatureClass pFeatureClass, string pFieldName, esriFieldType pFieldType, int pLength)
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
                logger.LogMessage(ServerLogger.msgType.debug, "add field to output", 8000,
                        "Couldn't get schema lock to add field " + pFieldName + " to output!" + ex.Message);
            }
            finally
            {
                tSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
            }
            logger.LogMessage(ServerLogger.msgType.debug, "AddAField", 99,
                                                 "Added field: " + pFieldName+", success: "+successful.ToString());

            return successful;
        }

        private bool AddAField(IFeatureClass pFeatureClass, string pFieldName, esriFieldType pFieldType)
        {
            bool successful = false;
            if (pFieldType == esriFieldType.esriFieldTypeString)
            {
                throw new ArgumentException("Length must be specified for a new string field", "pFieldType");
            }
            else
            {
                successful = AddAField(pFeatureClass, pFieldName, pFieldType,0);
            }
            return successful;
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
        private IGeoDataset OpenVectorDataset(string path, string featureclassname)
        {
            IWorkspaceFactory tVectorWorkspaceFactory = new FileGDBWorkspaceFactoryClass();
            IFeatureWorkspace tVectorWorkspace;
            IGeoDataset tVectorGDS = null;
            try
            {
                tVectorWorkspace = (IFeatureWorkspace)tVectorWorkspaceFactory.OpenFromFile(path, 0);
                IFeatureClass tVectorFC = tVectorWorkspace.OpenFeatureClass(featureclassname);
                tVectorGDS = (IGeoDataset)tVectorFC;
            }
            catch
            {
                logger.LogMessage(ServerLogger.msgType.error, "OpenVectorDataset", 8000, "Cannot open feature class " +
                       featureclassname + " in workspace " + path);
            }
            return tVectorGDS;
        }
        private IGeoDataset OpenRasterDataset(string path, string rastername)
        {
            IRasterWorkspace tWorkspace;
            try
            {
                // these two lines for GRID format rasters in a directory
                IWorkspaceFactory tWorkspaceFactory = new ESRI.ArcGIS.DataSourcesRaster.RasterWorkspaceFactoryClass();
                tWorkspace = tWorkspaceFactory.OpenFromFile(path, 0) as IRasterWorkspace;
                // these two lines for a filegdb raster workspace
                // IWorkspaceFactory2 tWorkspaceFactory = new FileGDBWorkspaceFactoryClass();
                //IRasterWorkspaceEx tWorkspace = tWorkspaceFactory.OpenFromFile(path, 0) as IRasterWorkspaceEx; 
            }
            catch (Exception e)
            {
                throw new ArgumentException("could not open raster workspace, exception detail was: " + e);
            }
            try
            {
                IGeoDataset tRaster = tWorkspace.OpenRasterDataset(rastername) as IGeoDataset;
                return tRaster;
            }
            catch(Exception e)
            {
                throw new ArgumentException("raster dataset " + rastername + " could not be opened", e);
            }
        }
        private IEnvelope GetAnalysisEnvelope(IPoint pLocation)
        {
            IGeoDataset tHydroAreaGDS = OpenVectorDataset(irn_vector_path,"HA");
            IFeatureClass tHydroAreaFC = (IFeatureClass)tHydroAreaGDS;
            ISpatialFilter tSpatialFilter = new SpatialFilterClass();
            tSpatialFilter.Geometry = pLocation as IGeometry;
            tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelWithin;
            tSpatialFilter.GeometryField = "SHAPE";
            IFeatureCursor tFeatureCursor = tHydroAreaFC.Search(tSpatialFilter, false);
            IFeature tHydroAreaFeature = tFeatureCursor.NextFeature(); // if there is none it will be null
            IEnvelope tHAEnvelope = tHydroAreaFeature.Extent;
            return tHAEnvelope;
        }
        private string GetTimeStamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

    }
    internal struct ExtractionLayerConfig
    {
        private readonly int m_layerID;
       // private readonly LayerTypes m_layerType;
        private readonly ExtractionTypes m_extractionType;
        private readonly string m_paramName;
        private readonly int m_categoryField;
        private readonly int m_valueField;
        private readonly int m_measureField;
        internal int LayerID { get { return m_layerID; } }
        //internal LayerTypes LayerType { get { return m_layerType; } }
        internal ExtractionTypes ExtractionType { get { return m_extractionType; } }
        internal string ParamName { get { return m_paramName; } }
        internal int CategoryField {get {return m_categoryField;}}
        internal int ValueField {get {return m_valueField;}}
        internal int MeasureField {get {return m_measureField;}}
        internal bool HasCategories {get {return m_categoryField != -1;}}
        internal bool HasValues {get {return m_valueField != -1;}}
        internal bool HasMeasures {get {return m_measureField != -1;}}

        public ExtractionLayerConfig(int id,  ExtractionTypes extractiontype, string paramname,
                int CategoryFieldId,int ValueFieldId, int MeasureFieldId)
        {
            this.m_layerID = id;
           // this.m_layerType = layertype;
            this.m_extractionType = extractiontype;
            this.m_paramName = paramname;
            this.m_categoryField = CategoryFieldId;
            this.m_valueField = ValueFieldId;
            this.m_measureField = MeasureFieldId;
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
  
}
            #endregion