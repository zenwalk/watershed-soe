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
        private static ServerLogger logger = new ServerLogger();
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
        
        /// <summary>
        /// Summarise the features of an input feature class that fall within an input polygon.
        /// Input features can be points, lines, or polygons. A value field and a category field can be provided.
        /// If a category field is provided then this will be used like a GROUP BY clause in SQL and return will be broken down by
        /// category, in addition to the overall totals.
        /// If a value field is provided then it will be totalled (by category if provided), e.g. population in a polygon.
        /// But as some line / polygon features may be only partially contained we need to decide how to handle the values on those.
        /// Currently we just scale based on the proportion of the original feature that is included but there would be
        /// alternatives: count all or nothing based on inclusion of majority of feature, or centre of feature. Not yet implemented.
        /// </summary>
        /// <param name="pInputPolygon">
        /// The IPolygon which will be used to clip and summarise the features from the second parameter
        /// </param>
        /// <param name="pInputFeatures">
        /// The features which will be clipped and summarised. IFeatureClass that must be of ShapeType 
        /// esriGeometryPoint, esriGeometryPolyline or esriGeometryPolygon
        /// </param>
        /// <param name="pCategoryFieldNum">
        /// ID (integer) of a field in the feature class containing values by which the results should be grouped. Can be 
        /// any field type but integer, string, etc are recommended. Value of -1 means no summation by category will occur.
        /// Set to -1 to not do this summary
        /// </param>
        /// <param name="pValueFieldNum">
        /// ID (integer of a field in the feature class containing values by which the results should be totalled. For example
        /// population of counties, value of land parcels. Done in addition to totalling area / length / count.
        /// Set to -1 to not do this summary
        /// </param>
        /// <param name="pMeasureFieldNum">
        /// ID (integer) of a field in the feature class containing pre-calculated values for area (polygons) or length (lines). 
        /// Can be used to speed calculation of these properties. They will be calculated manually for features partially 
        /// within the input polygon.
        /// </param>
        /// <returns>
        /// FeatureExtractionResult object containing:
        /// total feature count,
        /// total feature length / area (for lines / polygons),
        /// total feature value (from value field if provided),
        /// plus each of the above broken down by category if a category field is provided
        /// category results are each a dictionary of key=category value (as string), value=int or double
        /// </returns>
        internal static FeatureExtractionResult SummariseFeatures(IPolygon pInputPolygon, 
            //ExtractionLayerConfig pExtractionLayerConfig)
            IFeatureClass pInputFeatures,int pCategoryFieldNum, int pValueFieldNum, int pMeasureFieldNum)
        {
            esriGeometryType tFCType = pInputFeatures.ShapeType;
            // set up variables to build results
            Dictionary<string, int> tCategoryCounts = new Dictionary<string, int>();
            Dictionary<string, double> tCategoryTotals = new Dictionary<string, double>(),
                tCategoryMeasures = new Dictionary<string, double>();
            double tTotalMeasure = 0, tTotalValue = 0;
            int tTotalCount = 0;
            // variables to control search
            bool hasCategories = pCategoryFieldNum != -1;
            bool hasMeasures = (pMeasureFieldNum != -1 && 
                (tFCType == esriGeometryType.esriGeometryPolygon || tFCType == esriGeometryType.esriGeometryPolyline));
            
            bool hasPreCalcMeasures = hasMeasures && (pMeasureFieldNum != -1);
            bool hasValues = false;
            if (pValueFieldNum != -1)
            {
                // only numeric fields will be totalled
                esriFieldType tValueFieldType = pInputFeatures.Fields.get_Field(pValueFieldNum).Type;
                if (tValueFieldType == esriFieldType.esriFieldTypeDouble ||
                    tValueFieldType == esriFieldType.esriFieldTypeInteger ||
                    tValueFieldType == esriFieldType.esriFieldTypeSingle ||
                    tValueFieldType == esriFieldType.esriFieldTypeSmallInteger)
                {
                    hasValues = true;
                }
            }
            // use a spatial filter to do the geographic selection
            ISpatialFilter tSpatialFilter = new SpatialFilterClass();
            tSpatialFilter.Geometry = pInputPolygon as IGeometry;
            // first we will select all features wholly within the polygon. We don't need to do anything special with these
            // just use them as is. This applies for points, lines and polys. It is the only thing required for points.
            tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;
            tSpatialFilter.GeometryField = "SHAPE";
            // safe to use a recycling cursor: we are not maintaining a reference to features across multiple calls to NextFeature
            IFeatureCursor tFeatureCursor = pInputFeatures.Search(tSpatialFilter, true);
            IFeature tThisFeature = tFeatureCursor.NextFeature();
            try
            {
                while (tThisFeature != null)
                {
                    tTotalCount += 1;
                    double tMeasure = 0;
                    if (hasPreCalcMeasures)
                    {
                        try
                        {
                            tMeasure = (double)tThisFeature.get_Value(pMeasureFieldNum);
                        }
                        catch
                        {
                            hasPreCalcMeasures = false;
                            if (tFCType == esriGeometryType.esriGeometryPolyline)
                            {
                                IPolyline tFeatureAsLine = tThisFeature.Shape as IPolyline;
                                tMeasure = tFeatureAsLine.Length;
                            }
                            else if (tFCType == esriGeometryType.esriGeometryPolygon)
                            {
                                IArea tFeatureAsArea = tThisFeature.Shape as IArea;
                                tMeasure = tFeatureAsArea.Area;
                            }
                        }
                    }
                    else
                    {
                        if (tFCType == esriGeometryType.esriGeometryPolyline)
                        {
                            IPolyline tFeatureAsLine = tThisFeature.Shape as IPolyline;
                            tMeasure = tFeatureAsLine.Length;
                        }
                        else if (tFCType == esriGeometryType.esriGeometryPolygon)
                        {
                            IArea tFeatureAsArea = tThisFeature.Shape as IArea;
                            tMeasure = tFeatureAsArea.Area;
                        }
                    }
                    tTotalMeasure += tMeasure;
                    if (hasCategories)
                    {
                        // get the category / class of this featue
                        string tCategory = tThisFeature.get_Value(pCategoryFieldNum).ToString();
                        // placeholders for the dictionary lookups (out variables)
                        int tCurrentCategoryCount;
                        double tCurrentCategoryMeasure;
                        // add 1 to the appropriate category count in the category counts dictionary
                        if (tCategoryCounts.TryGetValue(tCategory, out tCurrentCategoryCount))
                        {
                            tCategoryCounts[tCategory] = tCurrentCategoryCount + 1;
                        }
                        else
                        {
                            tCategoryCounts[tCategory] = 1;
                        }
                        if (tCategoryMeasures.TryGetValue(tCategory, out tCurrentCategoryMeasure))
                        {
                            tCategoryMeasures[tCategory] = tCurrentCategoryMeasure + tMeasure;
                        }
                        else
                        {
                            tCategoryMeasures[tCategory] = tMeasure;
                        }
                        if (hasValues)
                        {
                            // i.e. look up the value from another field, other than just the feature's length/area and count
                            double tCurrentCategoryTotal;
                            double tCurrentArcValue = (double)tThisFeature.get_Value(pValueFieldNum);
                            tTotalValue += tCurrentArcValue;
                            if (tCategoryTotals.TryGetValue(tCategory, out tCurrentCategoryTotal))
                            {
                                tCategoryTotals[tCategory] = tCurrentCategoryTotal + tCurrentArcValue;
                            }
                            else
                            {
                                tCategoryTotals[tCategory] = tCurrentArcValue;
                            }
                        }
                    }
                    else if (hasValues)
                    {
                        double tCurrentArcValue = (double)tThisFeature.get_Value(pValueFieldNum);
                        tTotalValue += tCurrentArcValue;
                    }
                    tThisFeature = tFeatureCursor.NextFeature();
                }
                // now for lines and polygons we need to process the features that are partially inside the polygon.
                // this process would work on all the features but there is no point doing expensive intersections where we 
                // don't need to, so we did the wholly-contained features separately
                // we need to find the features where there is an intersection between the polygon boundary and the feature's
                // interior. Could use shape description language but this is covered by available spatialrelenum values
                bool doPartialFeatures = ((tFCType == esriGeometryType.esriGeometryPolyline ||
                                          tFCType == esriGeometryType.esriGeometryPolygon) ); // a point is either in or out!
                                         // not yet implemented: control how partially-intersecting features are handled
                                         //&& pFeatureIntersectionMode != IntersectingFeatureSelectionMode.SelectNoPartialFeatures);
                if (doPartialFeatures){
                    if (tFCType == esriGeometryType.esriGeometryPolyline)
                    {
                        // "A polyline and a polygon cross if they share a polyline or a point (for vertical line) in common on the 
                        // interior of the polygon which is not equivalent to the entire polyline."
                        tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses;
                    }
                    else if (tFCType == esriGeometryType.esriGeometryPolygon)
                    {
                        // "Two geometries overlap if the region of their intersection is of the same dimension as the geometries involved
                        // and is not equivalent to either of the geometries."
                        tSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelOverlaps;
                    }
                    // no longer safe to use recycling cursor as we're going to tinker with the features returned
                    tFeatureCursor = pInputFeatures.Search(tSpatialFilter, false);
                    tThisFeature = tFeatureCursor.NextFeature();
                    ITopologicalOperator tInputAsTopoOp = pInputPolygon as ITopologicalOperator;
                    int tNumberCrossingBoundary = 0;
                    while (tThisFeature != null)
                    {
                        // return (so track) the number of partially-included features separately from the overall total
                        tNumberCrossingBoundary += 1;
                        // either the length or area of the intersected feature:
                        double tMeasure = 0;
                        // either the length or area of entire original intersected feature
                        // (to get proportion that's included):
                        double tOriginalMeasure = 0;
                        // do the 
                        IGeometry tFeatureGeometry = tThisFeature.ShapeCopy;
                        if (tFCType == esriGeometryType.esriGeometryPolyline)
                        {
                            IPolyline tArcEntire = tFeatureGeometry as IPolyline;
                            tOriginalMeasure = tArcEntire.Length;
                            IPolyline tArcInside = tInputAsTopoOp.Intersect(tFeatureGeometry, esriGeometryDimension.esriGeometry1Dimension) as IPolyline;
                            tMeasure = tArcInside.Length;
                        }
                        else
                        {
                            IArea tAreaEntire = tFeatureGeometry as IArea;
                            tOriginalMeasure = tAreaEntire.Area;
                            IArea tAreaInside = tInputAsTopoOp.Intersect(tFeatureGeometry, esriGeometryDimension.esriGeometry2Dimension) as IArea;
                            tMeasure = tAreaInside.Area;
                        }
                        tTotalMeasure += tMeasure;
                        if (hasCategories)
                        {
                            string tCategory = tThisFeature.get_Value(pCategoryFieldNum).ToString();
                            int tCurrentCategoryCount;
                            double tCurrentCategoryMeasure;
                            if (tCategoryCounts.TryGetValue(tCategory, out tCurrentCategoryCount))
                            {
                                tCategoryCounts[tCategory] = tCurrentCategoryCount + 1;
                            }
                            else
                            {
                                tCategoryCounts[tCategory] = 1;
                            }
                            if (tCategoryMeasures.TryGetValue(tCategory, out tCurrentCategoryMeasure))
                            {
                                tCategoryMeasures[tCategory] = tCurrentCategoryMeasure + tMeasure;
                            }
                            else
                            {
                                tCategoryMeasures[tCategory] = tMeasure;
                            }
                            if (hasValues)
                            {
                                // how should we handle a value field in an intersected feature? we can't, for certain,
                                // as we don't know what they mean. We'll just assume that it scales proportionally with the
                                // proportion of the original feature's length / area that is included.
                                // The raster equivalent is to count all or none based on cell centre, so maybe we should count
                                // all or none based on centroid??
                                double tCurrentCategoryTotal;
                                double tCurrentFeatureValue = (double)tThisFeature.get_Value(pValueFieldNum);
                                double tScaledFeatureValue = (tMeasure / tOriginalMeasure) * tCurrentFeatureValue;
                                tTotalValue += tScaledFeatureValue;
                                if (tCategoryTotals.TryGetValue(tCategory,out tCurrentCategoryTotal))
                                {
                                    tCategoryTotals[tCategory] = tCurrentCategoryTotal + tScaledFeatureValue;
                                }
                                else 
                                {
                                    tCategoryTotals[tCategory] = tScaledFeatureValue;
                                }
                            }
                        }
                        else if (hasValues)
                        {
                            double tCurrentFeatureValue = (double)tThisFeature.get_Value(pValueFieldNum);
                            double tScaledFeatureValue = (tMeasure / tOriginalMeasure)*tCurrentFeatureValue;
                            tTotalValue += tScaledFeatureValue;
                        }
                    tThisFeature = tFeatureCursor.NextFeature();
                    }
                    }
                double? outMeasure;
                double? outValue;
                if (hasMeasures) { outMeasure = tTotalMeasure; }
                else { outMeasure = null; }
                if (hasValues) { outValue = tTotalValue; }
                else { outValue = null; }
                FeatureExtractionResult tResult = new FeatureExtractionResult(
                    tTotalCount,
                    outMeasure,
                    outValue,
                    tCategoryCounts,
                    tCategoryMeasures,
                    tCategoryTotals
                );
                return tResult;

            }
            catch (Exception ex)
            {
                logger.LogMessage(ServerLogger.msgType.debug, "process features", 99, "error summarising features in " +
                           pInputFeatures.AliasName+" Detail: " + ex.StackTrace + " " + ex.Message);
                return new FeatureExtractionResult("An error occurred with extraction from "+pInputFeatures.AliasName);
            }
        }
       
    }
    internal enum IntersectingFeatureSelectionMode
    {
        SelectNoPartialFeatures,
        SelectByCentre,
        SelectByMajority,
        SelectProportionally
    }
    internal struct FeatureExtractionResult
    {
        private readonly bool m_successful;
        private readonly bool m_hascategories;
        private readonly bool m_hasvalues;
        private readonly bool m_hasmeasures;
        private readonly Dictionary<string,int> m_categorycounts;
        private readonly Dictionary<string,double> m_categorymeasures;
        private readonly Dictionary<string,double> m_categorytotals;
        private readonly int m_totalcount;
        private readonly double m_totalvalue;
        private readonly double m_totalmeasure;
        private readonly string m_errormessage;

        internal Dictionary<string,int> CategoryCounts { get { return m_categorycounts; } }
        internal Dictionary<string,double> CategoryMeasures { get { return m_categorymeasures; } }
        internal Dictionary<string,double> CategoryTotals { get { return m_categorytotals; } }
        internal int TotalCount { get { return m_totalcount; } }
        internal double TotalMeasure { get { return m_totalmeasure; } }
        internal double TotalValue { get { return m_totalvalue; } }
        internal bool ExtractionSuccessful { get { return m_successful; } }
        internal bool HasCategories { get { return m_hascategories; } }
        internal bool HasValues { get { return m_hasvalues; } }
        internal bool HasMeasures { get { return m_hasmeasures; } }

        internal string ErrorMessage { get { return m_errormessage; } }

        public FeatureExtractionResult(int TotalCount, double? TotalMeasure, double? TotalValue,
            Dictionary<string,int> CategoryCounts, Dictionary<string,double> CategoryMeasures, 
            Dictionary<string,double> CategoryTotals)
        {
            this.m_successful = true;
            // all parameters except for totalcount can be null
            // i.e. in the case where we have just counted the total number of points with no categories
            // or values
            this.m_totalcount = TotalCount;
            if (TotalMeasure.HasValue)
            {
                this.m_totalmeasure = TotalMeasure.Value;
                this.m_hasmeasures = true;
            }
            else
            {
                this.m_totalmeasure = -1;
                this.m_hasmeasures = false;
            }
            if (TotalValue.HasValue)
            {
                this.m_totalvalue = TotalValue.Value;
                this.m_hasvalues = true;
            }
            else
            {
                this.m_totalvalue = -1;
                this.m_hasvalues = false;
            }
            this.m_categorycounts = CategoryCounts;
            if (CategoryCounts != null)
            {
                this.m_hascategories = true;
            }
            else
            {
                this.m_hascategories = false;
            }
            this.m_categorymeasures = CategoryMeasures;
            if (CategoryMeasures != null)
            {
                this.m_hasmeasures = true;
            }
            else
            {
                // it must be points
                this.m_hasmeasures=false;
            }
            this.m_categorytotals = CategoryTotals;
            this.m_errormessage = "No errors occurred";
        }
        public FeatureExtractionResult(string ErrorMessage)
        {
            this.m_successful = false;
            this.m_errormessage = ErrorMessage;
            this.m_totalcount = -1;
            this.m_totalmeasure = -1;
            this.m_totalvalue = -1 ;
            this.m_hasmeasures = false;
            this.m_hascategories = false;
            this.m_hasvalues = false;
            this.m_categorycounts = null;
            this.m_categorymeasures = null;
            this.m_categorytotals = null;

        }
    }
}
