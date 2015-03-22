# What #
This is a REST Server Object Extension (SOE) for ArcGIS Server. It has the following key purposes:
  * to enable derivation of a river catchment / watershed (the area draining to a given input point)
  * based on a polygon feature (which may be the watershed produced above, or may be passed in by the user) to summarise other layers in the map service.

Output either takes the form of a polygon graphic, with attributes of the other summarised datasets "flattened" onto it as attributes, or of a structured JSON object.

It is written by Harry Gibson at [CEH Wallingford](http://www.ceh.ac.uk) and is copyright NERC.

# Why #
It is enormously faster to produce watersheds in a SOE rather than by publishing a geoprocessing service.

The ability to summarise datasets from an input polygon quickly and simply through a REST interface opens the way to easy and fast summarising of data for a user defined area e.g. through the ESRI Javascript API.

# How #
The SOE is written in C#.

When enabled on a map service the SOE will expose a createWatershed operation if the required Flow Accumulation and Flow Direction layers have been configured in the service properties (an ArcCatalog property page is included to do this but the current version is incomplete - see info below).

It is strongly recommended that a third layer is included for catchment definition, to provide features used to determine search extent (e.g. in the UK, Hydrometric Areas - we can be sure that no catchment will be larger than its containing Hydrometric Area feature).

The SOE will also expose an ExtractByPolygon operation regardless of whether the watershed definition layers are available. It can therefore be used to summarise datasets outside of a hydrological context.

# Configuration #
The SOE summarises 5 types of data layer:
  * Continuous raster. The SOE identifies these as those raster layers which are symbolised with a stretch renderer. It will return the min, max and mean values.
  * Categorical raster. The SOE identifies these as those raster layers which are symbolised with a unique or discrete renderer. It will return a count of how many cells have each value.
  * Point features. It will return a count of features in the polygon. If there is a field with the Alias Name "CATEGORY" then the values of this field will be used to group / split the summary. If you select a field with unique values for this (e.g. ObjectID) then this will have the effect of giving you a list of the individual features.
  * Line features. It will return a count of features in the polygon (including any crossing the border), and their total length (trimmed to the boundary). This is optionally broken down by a category / grouping field as for point features.
  * Polygon features. As for lines but with area instead of length.

How the SOE summarises each layer depends how it is configured in the map document. Raster layers are chosen according to how they are symbolised as described above. Features will be broken down by category if there is a field with the Alias Name "CATEGORY".

Each layer will be exposed as a parameter (so that a request can ask for it to be extracted, or not). The parameter name will be determined by the first 6 or fewer characters of each layer name or description, delimited by a ':'.
E.g. a layer named "ELEV: Elevation of land surface" will be exposed as a parameter called ELEV.

An ArcCatalog page is under development to configure this stuff without needing to do it in the map document. For now it doesn't work. Use the earlier version of the property page to configure Flow Accumulation and Flow Direction layers to enable watershed extraction.
