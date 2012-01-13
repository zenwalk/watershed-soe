using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SpatialAnalyst;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.GeoAnalyst;
namespace WatershedSOE
{
    [Guid("3f6ba7bd-7605-43bc-9f6c-fcac916d7ba9")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("WatershedSOE.WatershedDetailExtraction")]
    public class WatershedDetailExtraction
    {
        internal static double FindTotalUpstreamLength(IPolygon pCatchmentBoundary, IGeoDataset pRiversGDS)
        {
            ServerLogger logger = new ServerLogger();
            logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "Summarising upstream lengths...");
            try
            {
                ISpatialFilter tSpatialFilter = new SpatialFilterClass();
                tSpatialFilter.Geometry = pCatchmentBoundary as IGeometry;
                tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;
                tSpatialFilter.GeometryField = "SHAPE";
                IFeatureClass tRiversFC = (IFeatureClass)pRiversGDS;
                IFeatureCursor tFeatureCursor = tRiversFC.Search(tSpatialFilter, false);
                IFeature tThisArc = tFeatureCursor.NextFeature();
                double tTotalLength = 0;
                int tNumberInside = 0;
                while (tThisArc != null)
                {
                    try     {tTotalLength += (double)tThisArc.get_Value(tThisArc.Fields.FindField("LENGTH"));}
                    catch   {logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "problem parsing length field...");}
                    finally { tNumberInside += 1; tThisArc = tFeatureCursor.NextFeature(); }
                }
                logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "Number of arcs inside boundary: " + tNumberInside.ToString());
                // NB This gave the length for arcs within the catchment. Now do ones which cross it
                // to get the one the site is on (and any others which cross the boundary); for each of these
                // intersect it with catchment to get the portion inside
                tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses;
                tFeatureCursor = tRiversFC.Search(tSpatialFilter, false);
                tThisArc = tFeatureCursor.NextFeature();
                ITopologicalOperator tCatchmentTopoOp = pCatchmentBoundary as ITopologicalOperator;
                int tNumberCrossing = 0;
                while (tThisArc != null)
                {
                    IGeometry tArcGeometry = tThisArc.ShapeCopy;
                    IPolyline tArcInside = tCatchmentTopoOp.Intersect(tArcGeometry,esriGeometryDimension.esriGeometry1Dimension) as IPolyline;
                    tTotalLength += tArcInside.Length;
                    tNumberCrossing += 1;
                    tThisArc = tFeatureCursor.NextFeature();
                }
                logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "Number of arcs crossing boundary: " + tNumberCrossing.ToString());
                logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "Got total upstream length of " + tTotalLength.ToString());

                return tTotalLength;
            }
            catch (Exception e)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, "Exception caused in summarising upstream length");
                logger.LogMessage(ServerLogger.msgType.debug, "find total upstream length", 99, e.Message + " " + e.StackTrace + " " + e.TargetSite);
                return 0;
            }
        }
        internal static Dictionary<int, double> SummariseCategoricalRaster(IGeoDataset pCatchment, IGeoDataset pClipRaster)
        {
            // set the analysis extent to be that of the polygon
            ServerLogger logger = new ServerLogger();
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseCategoricalRaster", 99, "Categorical raster clip beginning..");
            IEnvelope tAnalysisExtent = pCatchment.Extent;
            //IEnvelope tAnalysisExtent = pCatchment.Envelope; IF CATCMENT IS IPOLYGON
            IRasterAnalysisEnvironment tRasterAnalysisEnvironment = new RasterAnalysisClass();
            object tAnalysisEnvelopePointlesslyCastedToObject = (System.Object)tAnalysisExtent;
            object tAnotherPointlessMissingObject = Type.Missing;
            object tSnapObject = (System.Object)pClipRaster;
            tRasterAnalysisEnvironment.SetExtent(esriRasterEnvSettingEnum.esriRasterEnvValue,
                ref tAnalysisEnvelopePointlesslyCastedToObject, ref tSnapObject);
            tRasterAnalysisEnvironment.SetAsNewDefaultEnvironment();
            // extract the subset of the raster
            IExtractionOp2 tExtractionOp = new RasterExtractionOpClass();
            // note we are basing the extraction on a raster (in an IGeoDataset) rather than an IPolygon.
            // That's because the catchment polygon may be multipart, and the operation doesn't work with multipart.
            // Doing each part separately would be a faff. And raster mask extraction is probably faster since the
            // polygon is converted internally to a grid anyway.
            IGeoDataset tClipped = tExtractionOp.Raster(pClipRaster, pCatchment);
            // POLYGON VERSION: tExtractionOp.Polygon(pClipRaster,pPolygon,true)

            logger.LogMessage(ServerLogger.msgType.debug, "SummariseCategoricalRaster", 99, "Categorical raster summary beginning..");
            // fiddle about to get to the extracted raster's attribute table
            IRasterBandCollection tClippedAsBandCollection = tClipped as IRasterBandCollection;
            IRasterBand tClippedBand = tClippedAsBandCollection.Item(0);
            ITable tClippedTable = tClippedBand.AttributeTable;
            // pass through the table once to get the total cell count
            ICursor tTableCursor = tClippedTable.Search(null, false);
            IRow tTableRow = tTableCursor.NextRow();
            double totalcount = 0.0; // store as a double so the percentage division results in a double later
            while (tTableRow != null)
            {
                totalcount += (int)tTableRow.get_Value(tTableRow.Fields.FindField("COUNT"));
                tTableRow = tTableCursor.NextRow();
            }
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseCategoricalRaster", 99, "Counted total cells: " + totalcount.ToString());
            // go through the table again to get the percentage of total of each value
            tTableCursor = tClippedTable.Search(null, false);
            tTableRow = tTableCursor.NextRow();
            Dictionary<int, double> tResultsAsPct = new Dictionary<int, double>();
            //Dictionary<int,double> tResultsAsArea = new Dictionary<int,double>();
            IRasterProps tRasterProps = tClippedBand as IRasterProps;
            double tCellsizeX = tRasterProps.MeanCellSize().X;
            double tCellsizeY = tRasterProps.MeanCellSize().Y;
            while (tTableRow != null)
            {
                int tCode = (int)tTableRow.get_Value(tTableRow.Fields.FindField("VALUE"));
                int tCount = (int)tTableRow.get_Value(tTableRow.Fields.FindField("COUNT"));
                double tPercent = tCount / totalcount * 100;
                tResultsAsPct[tCode] = tPercent;
                //tResultsAsArea[tCode] = tCount * tCellsizeX * tCellsizeY;
                tTableRow = tTableCursor.NextRow();
            }
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseCategoricalRaster", 99, "Categorical raster summary done..");

            // restore the default (full) raster analysis environment else the next thing will probably be a catchment
            // definition with no extent set, according to sod's law, and it won't work
            tRasterAnalysisEnvironment.RestoreToPreviousDefaultEnvironment();
            // TODO build an overall results object and dump our dictionary into that
            return tResultsAsPct;
        }
        internal static Dictionary<string, double> SummariseContinuousRaster(IGeoDataset pCatchment, IGeoDataset pClipRaster)
        {
            ServerLogger logger = new ServerLogger();
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseCategoricalRaster", 99, "Continuous raster clip beginning..");
            // set analysis extent
            //IEnvelope tAnalysisExtent = pCatchment.Envelope;
            IEnvelope tAnalysisExtent = pCatchment.Extent;
            IRasterAnalysisEnvironment tRasterAnalysisEnvironment = new RasterAnalysisClass();
            object tAnalysisEnvelopePointlesslyCastedToObject = (System.Object)tAnalysisExtent;
            object tAnotherPointlessMissingObject = Type.Missing;
            object tSnapObject = (System.Object)pClipRaster;
            tRasterAnalysisEnvironment.SetExtent(esriRasterEnvSettingEnum.esriRasterEnvValue,
                ref tAnalysisEnvelopePointlesslyCastedToObject, ref tSnapObject);
            tRasterAnalysisEnvironment.SetAsNewDefaultEnvironment();
            // extract raster subset
            IExtractionOp2 tExtractionOp = new RasterExtractionOpClass();
            IGeoDataset tClipped;
            //if (pCatchment.ExteriorRingCount == 1)
            //{
            //tClipped = tExtractionOp.Polygon(pClipRaster, pCatchment, true);
            tClipped = tExtractionOp.Raster(pClipRaster, pCatchment);
            //}
            //else
            //{
             //   IRasterConvertHelper tRasterConvertHelper = new RasterConvertHelperClass
            //}
            IRasterBandCollection tClippedAsBandCollection = tClipped as IRasterBandCollection;
            IRasterBand tClippedBand = tClippedAsBandCollection.Item(0);
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseContinuousRaster", 99, "Continuous raster clipped, checking stats...");
            bool tHasStatistics;
            tClippedBand.HasStatistics(out tHasStatistics);
            // what on earth? why not just have a bool return type to the HasStatistics method??!
            if (!tHasStatistics)
            {
                tClippedBand.ComputeStatsAndHist();
            }
            IRasterStatistics tClippedStats = tClippedBand.Statistics;
            //tClippedStats.Recalculate();
            logger.LogMessage(ServerLogger.msgType.debug, "SummariseContinuousRaster", 99, "Continuous raster stats made, recording info...");
            Dictionary<string, double> tResults = new Dictionary<string, double>();
            tResults["Mean"] = tClippedStats.Mean;
            tResults["Max"] = tClippedStats.Maximum;
            tResults["Min"] = tClippedStats.Minimum;
            //NB Median isn't available with floating data. (Neither are majority,minority,variety). Would need
            //to convert to int by multiplying raster first.
            //tResults["Median"] = tClippedStats.Median;
            
            tRasterAnalysisEnvironment.RestoreToPreviousDefaultEnvironment();
            
            return tResults;
        }
        internal void SummarisePolygonFeatures(IPolygon pCatchment, IGeoDataset pPolygonFeatures, string pFieldName)
        {
            // we will use the ITopologicalOperator.Intersect method to get the intersection of each input
            // polygon feature with the catchment; this is unlike the old IRN which used geoprocessing to clip
            // to a whole new feature class and then summarised it.
            // First select only those input features that are relevant using a spatial filter. 
            ISpatialFilter tSpatialFilter = new SpatialFilterClass();
            tSpatialFilter.Geometry = pCatchment;
            tSpatialFilter.GeometryField = "SHAPE";
            tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

            // update this, this is just so i can type the rest in
            IFeatureClass tPolygonFC = pPolygonFeatures as IFeatureClass;
            
            // get a feature cursor that will give the intersecting features
            IFeatureCursor tFeatureCursor = tPolygonFC.Search(tSpatialFilter, true);
            IFeature tFeature = tFeatureCursor.NextFeature();
            int tSummaryFieldIdx;
            try
            {
                tSummaryFieldIdx = tFeature.Fields.FindField(pFieldName);
            }
            catch
            {
                ServerLogger logger = new ServerLogger();
                logger.LogMessage(ServerLogger.msgType.debug, "SummarisePolygonFeatures",8000,
                    "Cannot find field "+pFieldName+" in polygon features");
                tSummaryFieldIdx = 0;
            }
            Dictionary<string,double> tResults = new Dictionary<string,double>();
            object tPointlessMissingObject = Type.Missing;
            while (tFeature != null)
            {
                ITopologicalOperator tTopoOperator = tFeature.ShapeCopy as ITopologicalOperator;
                //tGeomBag.AddGeometry(poly.Boundary, ref tPointlessMissingObject, ref tPointlessMissingObject);
                IGeometry tIntersectedShape = tTopoOperator.Intersect(pCatchment,esriGeometryDimension.esriGeometry2Dimension);
                IArea tArea = tIntersectedShape as IArea;
                double tFeatureArea = tArea.Area;
                string tFeatureId = tFeature.get_Value(tSummaryFieldIdx).ToString();
                if (tResults.ContainsKey(tFeatureId))
                {
                    tResults[tFeatureId] += tFeatureArea;
                }
                else
                {
                    tResults.Add(tFeatureId,tFeatureArea);
                }
                //tIntersectedFeature.
                tFeature = tFeatureCursor.NextFeature();
            }
        }
    }
}
