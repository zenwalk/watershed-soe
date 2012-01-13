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

       // private string accum_prefix = "FA_";
       // private string direction_prefix = "FD_";
        private string accum_name = "fac";
        private string dir_name = "fdr";
       // private string m_FlowAccLayerName;
       // private string m_FlowDirLayerName;
       // private IGeoDataset m_FlowDirDataset;
       // private IGeoDataset m_FlowAccDataset;

        public WatershedSOE()
        {
            soe_name = "WatershedSOE";
            logger = new ServerLogger();
            logger.LogMessage(ServerLogger.msgType.infoStandard,"startup",8000,"soe_name is "+soe_name);
            reqHandler = new SoeRestImpl(soe_name, CreateRestSchema()) as IRESTRequestHandler;
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
            /*
            try
            {
                logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "watershed SOE constructor");
                if (props != null)
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "watershed SOE props present");
                    configProps = props;
                    string propstring = props.ToString();
                    System.Object tnames;
                    System.Object tvalues;
                    props.GetAllProperties(out tnames, out tvalues);
                    
                    foreach (string i in (String[])tnames)
                    {
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "prop name is "+i);
                        logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "prop values are "+tvalues.ToString());
                    
                    }
                    
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "watershed SOE props null");
                }
                if (props.GetProperty("FlowAccum") != null)
                {
                    m_FlowAccLayerName = props.GetProperty("FlowAccum") as string;
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: got FlowAccum");

                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: FlowAccum property missing");
                    throw new ArgumentNullException();
                }
                if (props.GetProperty("FlowDir") != null)
                {
                    m_FlowDirLayerName = props.GetProperty("FlowDir") as string;
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: got FlowDir");
                }
                else
                {
                    logger.LogMessage(ServerLogger.msgType.infoStandard, "Construct", 8000, "WSH: Flowdir property missing");
                    throw new ArgumentNullException();
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
                // get the flow accum and direction datasets: we only need to do this at startup not each time
                IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
                string mapName = mapServer.DefaultMapName;
                IMapLayerInfo layerInfo;
                IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
                int c = layerInfos.Count;
                int acc_layerIndex=0;
                int dir_layerIndex=0;
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
                    if(acc_layerIndex != 0 && dir_layerIndex != 0)
                    {
                        break;
                    }
                }
                IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
                IRaster tFDR = dataAccess.GetDataSource(mapName,dir_layerIndex) as IRaster;
                m_FlowDirDataset =  tFDR as IGeoDataset;
                IRaster tFAR = dataAccess.GetDataSource(mapName,acc_layerIndex) as IRaster;
                m_FlowAccDataset = tFAR as IGeoDataset;
                if(m_FlowDirDataset == null || m_FlowAccDataset == null)
                {
                    logger.LogMessage(ServerLogger.msgType.error,"Construct", 8000,"Watershed SOE Error: layer not found");
                    return;
                }
            }
            catch
            {
                logger.LogMessage(ServerLogger.msgType.error,"Construct",8000,"Watershed SOE error: could not get the flow direction and accumulation datasets");
            }
       */ 
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

            RestOperation watershedOper = new RestOperation("createWatershed",
                                                      new string[] { "hydroshed_id", "location", "extent", "lcm2k", "elev","totalupstream" },
                                                      new string[] { "json" },
                                                      CreateWatershedHandler);

            rootRes.operations.Add(watershedOper);
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
            
            #region Do the catchment characteristic extractions, if any
            if (doLCM2000)
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
            }
            if (doTotalUpstream)
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
            }

            #endregion
            // The catchment feature now exists and has all attributes requested. Ready to go.
            // TODO - implement alternative return formats. Either do as follows OR if user chooses not to 
            // return geometry (e.g. for mobile app), build a JSON object manually that has LCM classes etc
            // as nested objects (will make it easier for client to handle LCM, Elevation ,etc, separately)
            IRecordSetInit returnRecSet = new RecordSetClass();
            returnRecSet.SetSourceTable(tWatershedPolyGDS as ITable, null);
            IRecordSet recset = returnRecSet as IRecordSet;
            byte[] jsonFeatures = Conversion.ToJson(recset);
            return jsonFeatures;
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
}
