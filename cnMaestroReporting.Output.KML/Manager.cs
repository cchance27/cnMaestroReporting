using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using Microsoft.Extensions.Configuration;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TimeSpan = System.TimeSpan;
using CommonCalculations;

namespace cnMaestroReporting.Output.KML
{
    public class Manager
    {
        public Settings settings = new();
        public Kml OutputKml { get; }
        private Style yellowIcon { get; }
        private Style greenIcon { get; }
        private Style whiteIcon { get; }
        private Style redIcon { get; }
        private Style towerIcon { get; }
        private Dictionary<double, Style> plotStyle { get; }

        private IEnumerable<SubscriberRadioInfo> Subscribers { get; }
        private IEnumerable<KeyValuePair<string, CnLocation>> Towers { get; }
        private IEnumerable<AccessPointRadioInfo> AccessPoints { get; }
        private double[] AccessChannels { get; }
        private Color32[] RandomColors { get; }

        public Manager(
            IConfigurationSection configSection,
            IEnumerable<SubscriberRadioInfo> subscribers,
            IEnumerable<KeyValuePair<string, CnLocation>> towers,
            IEnumerable<AccessPointRadioInfo> accesspoints)
        {
            // Bind our configuration
            configSection.Bind(settings);

            if (OutputKml == null)
                OutputKml = new Kml();

            redIcon = CreateIconStyle(nameof(redIcon), settings.Icons["Bad"]);
            yellowIcon = CreateIconStyle(nameof(yellowIcon), settings.Icons["Poor"]);
            greenIcon = CreateIconStyle(nameof(greenIcon), settings.Icons["Good"]);
            whiteIcon = CreateIconStyle(nameof(whiteIcon), settings.Icons["Unknown"]);
            towerIcon = CreateIconStyle(nameof(towerIcon), settings.Icons["Tower"]);
            Subscribers = subscribers.OrderBy(sm => sm.Name);
            Towers = towers.OrderBy(tower => tower.Key);
            AccessPoints = accesspoints.OrderBy(ap => ap.Name); // Name sorted dictionary.

            // Hardcoded random colors ugly but will work for now
            // TODO: Maybe an option so we can have colors by different parameters configurable, Random, Channel, Customer Count, Etc
            RandomColors = new Color32[] { new Color32(175, 0, 0, 255), new Color32(175, 64, 115, 255), new Color32(175, 128, 213, 255), new Color32(175, 0, 242, 194), new Color32(175, 162, 242, 0), new Color32(175, 242, 162, 0), new Color32(175, 140, 0, 75), new Color32(175, 170, 0, 255), new Color32(175, 0, 0, 242), new Color32(175, 102, 129, 204), new Color32(175, 0, 194, 242), new Color32(175, 121, 242, 186), new Color32(175, 191, 230, 115), new Color32(175, 166, 124, 41), new Color32(175, 230, 115, 191), new Color32(175, 213, 128, 255), new Color32(175, 51, 51, 204), new Color32(175, 48, 105, 191), new Color32(175, 0, 112, 140), new Color32(175, 0, 140, 37), new Color32(175, 145, 153, 38), new Color32(175, 255, 102, 0), new Color32(175, 140, 70, 117), new Color32(175, 95, 0, 178), new Color32(175, 115, 115, 229), new Color32(175, 0, 68, 127), new Color32(175, 128, 247, 255), new Color32(175, 57, 230, 57), new Color32(175, 230, 214, 0), new Color32(175, 166, 66, 0), new Color32(175, 179, 0, 143), new Color32(175, 97, 0, 242), new Color32(175, 70, 79, 140), new Color32(175, 61, 157, 242), new Color32(175, 0, 166, 155), new Color32(175, 88, 166, 0), new Color32(175, 255, 204, 0), new Color32(175, 204, 129, 102), new Color32(175, 255, 0, 238), new Color32(175, 143, 102, 204), new Color32(175, 0, 37, 140), new Color32(175, 83, 127, 166), new Color32(175, 77, 153, 148), new Color32(175, 98, 128, 64), new Color32(175, 166, 149, 83), new Color32(175, 255, 64, 89), new Color32(175, 141, 41, 166), new Color32(175, 63, 35, 140) };
            AccessChannels = accesspoints.GroupBy(ap => ap.Channel).Select(g => g.First()).Select(ap => ap.Channel).ToArray(); // Grab our list of unique channels
            plotStyle = new Dictionary<double, Style>();
            for (int i = 0; i < AccessChannels.Length; i++)
            {
                // Hacky way of supporting rolling over the colors to be more transpartent on the higher channels if we run out of colors
                int colorIndex = i % RandomColors.Length;
                int alpha = RandomColors[colorIndex].Alpha;
                if (i > RandomColors.Length)
                {
                    alpha /= (i / RandomColors.Length) + 1;
                }
                Color32 c = new((byte)alpha, RandomColors[colorIndex].Blue, RandomColors[colorIndex].Green, RandomColors[colorIndex].Red);
                
                plotStyle.Add(AccessChannels[i], CreatePlotStyle(AccessChannels[i], c));
            }
        }

        /// <summary>
        /// Create a PlotStyle based on channel
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Style CreatePlotStyle(double channel, Color32 color)
        {
            string name = $"plot_{channel}";
           var plotStyle = new Style()
            {
                Id = name,
                Polygon = new PolygonStyle()
                {
                    Outline = false,
                    Fill = true,
                    Color = color
                }
            };
            // NOT RANDOM
            // plotStyle.Polygon.ColorMode = ColorMode.Random;
            return plotStyle;
        }

        /// <summary>
        /// Create a IconStyle based on a settings config
        /// </summary>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        private static Style CreateIconStyle(string name, StyleConfig config)
        {
            var thisColor = System.Drawing.Color.FromName(config.Color);

            return new Style()
            {
                Id = name,
                Icon = new IconStyle()
                {
                    Icon = new IconStyle.IconLink(new Uri(config.Icon)),
                    Scale = config.IconScale
                },
                Label = new LabelStyle()
                {
                    Scale = config.TextScale,
                    Color = String.IsNullOrWhiteSpace(config.Color) ? new Color32(255, 255, 255, 255) : new Color32(thisColor.A, thisColor.B, thisColor.G, thisColor.R)
                }
            };
        }

        /// <summary>
        /// Passed a Dictionary of Names and Locations create the folders for the Towers, and then call the next step.
        /// </summary>
        /// <param name="tower"></param>
        /// <returns></returns>
        private Folder generateTower(KeyValuePair<string, CnLocation> tower)
        {
            {
                // Generate folder for containing all this towers ap, sectors, sms.
                var towerFolder = new Folder() { Name = tower.Key };

                // Generate our placemark for the towers themselves
                var towerPlacemark = new Placemark()
                {
                    Name = tower.Key,
                    Id = tower.Key,
                    Geometry = new Point()
                    {
                        Coordinate = new Vector(
                            latitude: (double)tower.Value.coordinates[1],
                            longitude: (double)tower.Value.coordinates[0])
                    },
                    StyleUrl = new Uri($"#{nameof(towerIcon)}", UriKind.Relative)
                };
                towerPlacemark.Visibility = settings.Icons["Tower"].Visibility;


                towerFolder.Description = new Description()
                {
                    Text = $"<![CDATA[Total Subscribers:{Subscribers.Where(sm => sm.Tower == tower.Key).Count()}<br/>Correct Lat/Long Subs: {Subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0 && sm.Tower == tower.Key).Count()}]]>"
                };

                // Loop through all the APs for this towers AP
                foreach (var ap in AccessPoints.Where(ap => ap.Tower == tower.Key))
                {
                    var sectorFolder = generateSector(ap, tower.Value);
                    towerFolder.AddFeature(sectorFolder);
                }

                towerFolder.AddFeature(towerPlacemark);
                return towerFolder;
            }
        }

        /// <summary>
        /// passed a sector build a folder containing the sector and all sm's attached to the sector.
        /// </summary>
        /// <param name="ap"></param>
        private Folder generateSector(AccessPointRadioInfo ap, CnLocation location)
        {
            // Create this sectors folder to hold all subscribers
            var sectorFolder = new Folder() { Name = ap.Name };

            // Tracker to know if we should show this sector by default (users are connected to it with bad signal)
            bool showSector = false;

            // Fetch all subscribers for this specific sector.
            var sectorSubscribers = Subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0 && sm.APName == ap.Name);

            sectorFolder.Description = CreateDescriptionFromObject(ap);

            // Loop through and create all the placemarks for these sector subscribers.
            if (settings.showSubscribers)
            {
                foreach (var sm in sectorSubscribers)
                {
                    // Check if the sm coordinates are realistic (within our configured range of the sector)
                    if (sm.DistanceGeoM <= settings.SmInvalidationRangeM)
                    {
                        var smPlacemark = GenerateSmPlacemark(sm);
                        if (smPlacemark.Visibility == true)
                            showSector = true;

                        sectorFolder.AddFeature(smPlacemark);
                    }
                }
            }

            if (ap.Azimuth != 999)
            {
                // Generate the plot to show the coverage based on the sectors azimuth and distance
                var sectorPlot = GenerateSectorPlot((double)location.coordinates[1], (double)location.coordinates[0], ap.Azimuth, 500, 90);
                var plotPlacemark = new Placemark() { Name = ap.Name + " Coverage", Geometry = sectorPlot, StyleUrl = new Uri($"#plot_{ap.Channel}", UriKind.Relative) };

                // We chose in settings to always show sector plots
                if (settings.alwaysShowSectorPlot == true) { showSector = true; };
                plotPlacemark.Visibility = showSector;
                sectorFolder.AddFeature(plotPlacemark);
            } else
            {
                Console.WriteLine($"AP Missing Azimuth: {ap.Name}");
            }

            return sectorFolder;
        }
         
        /// <summary>
        /// Generate a polygon that represents the coverage area of a sector based on azimuth and distance specified
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="azimuth"></param>
        /// <param name="meters"></param>
        private static Polygon GenerateSectorPlot(double latitude, double longitude, double azimuth, double distance, double sectorWidth, double sectorPointSplit = 8)
        {
            var sectorEdges = new LinearRing();
            var Coordinates = new CoordinateCollection
            {
                new Vector(latitude, longitude) // Starting point
            };

            // Find the first point of the arc
            var startAzi = azimuth - (sectorWidth / 2);
            if (startAzi < 0) { 
                startAzi = 360 + startAzi; 
            };

            for (double x = 0; x <= sectorWidth; x += (sectorWidth / sectorPointSplit)) // Split the total number of points on the arc 
            {
                var newAzi = startAzi + x > 360 ? startAzi + x - 360 : startAzi + x; 
                var newLocation = GeoCalc.LocationFromAzimuth(latitude, longitude, distance, newAzi); // Find this point on the arc lat/long
                Coordinates.Add(new Vector(newLocation.latitude, newLocation.longitude));
            }

            Coordinates.Add(new Vector(latitude, longitude)); // Close point
            sectorEdges.Coordinates = Coordinates;
            return new Polygon() { OuterBoundary = new OuterBoundary { LinearRing = sectorEdges } };
        }

        /// <summary>
        ///  Take a subscriberRadioInfo and return a nice SmPlacemark with all necesary subscriber information and location information.
        /// </summary>
        /// <param name="sm"></param>
        private Placemark GenerateSmPlacemark(SubscriberRadioInfo sm)
        {
            var smPlacemark = new Placemark()
            {
                Name = sm.Name,
                Id = sm.Name,
                Geometry = new Point()
                {
                    Coordinate = new Vector(
                                    latitude: (double)sm.Latitude,
                                    longitude: (double)sm.Longitude)
                }
            };


            smPlacemark.Description = CreateDescriptionFromObject(sm);

            // Apply icon to SM based on Signal Level
            if (sm.ApAPL <= settings.Icons["Bad"].SignalLevel || sm.SmAPL <= settings.Icons["Bad"].SignalLevel)
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(redIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Bad"].Visibility;
            }
            else if (sm.ApAPL <= settings.Icons["Poor"].SignalLevel || sm.SmAPL <= settings.Icons["Poor"].SignalLevel)
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(yellowIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Poor"].Visibility;
            }
            else if (sm.ApAPL <= settings.Icons["Good"].SignalLevel || sm.SmAPL <= settings.Icons["Good"].SignalLevel)
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(greenIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Good"].Visibility;
            }
            else
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(whiteIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Unknown"].Visibility;
            }

            return smPlacemark;
        }

        /// <summary>
        /// Create a well formatted CData Object Description to be used on an item in our KML this uses reflection and the KMLConfig attribute to manipulate data to display nicely.
        /// </summary>
        /// <param name="inputObject"></param>
        /// <returns></returns>
        static Description CreateDescriptionFromObject(Object inputObject)
        {
            var content = new StringBuilder();
            foreach (PropertyInfo item in inputObject.GetType().GetProperties())
            {
                var currentItemValue = item.GetValue(inputObject);
                var currentName = item.Name;
                var hidden = false;

                if (currentItemValue != null)
                {
                    if (item.GetType() == typeof(TimeSpan))
                        currentItemValue = FormatTimeSpan((TimeSpan)currentItemValue);

                    // Implement our KMLConfig Options for the object
                    var kmlConf = item.GetCustomAttribute<KMLConfig>();
                    if (kmlConf != null)
                    {
                        if (kmlConf.Hidden)
                            hidden = true;

                        if (string.IsNullOrWhiteSpace(kmlConf.Name) == false)
                            currentName = kmlConf.Name;

                        if (kmlConf.ConvertToUrl)
                            currentItemValue = "http://" + currentItemValue;

                        if (string.IsNullOrWhiteSpace(kmlConf.TrimAfter) == false)
                            currentItemValue = FormatTrimAfter((string)currentItemValue, kmlConf.TrimAfter);
                    }

                    if (!hidden)
                        content.Append($"<b>{currentName}</b>: { currentItemValue }<br />");
                }
            }

            return new Description() { Text = $"<![CDATA[{content}]]>" };
        }

        /// <summary>
        /// Trim text that comes after a specific character if it exists otherwise return value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="trimString"></param>
        /// <returns></returns>
        static string FormatTrimAfter(string value, string trimString) => value.Contains(trimString) ? value.Substring(0, value.IndexOf(trimString)).Trim() : value;

        /// <summary>
        /// Pretty format for a Timespan ##d ##hr ##m
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        static string FormatTimeSpan(System.TimeSpan timeSpan)
        {
            static string FormatPart(int quantity, string name) => quantity > 0 ? $"{quantity}{name}{(quantity > 1 ? "s" : "")}" : null;
            return string.Join(", ", new[] { FormatPart(timeSpan.Days, "d"), FormatPart(timeSpan.Hours, "hr"), FormatPart(timeSpan.Minutes, "m") }.Where(x => x != null));
        }
            
        /// <summary>
        /// Generate the full KML based on this class's properties
        /// </summary>
        public void GenerateKML()
        {
            // Base document to hold styles and items
            Document doc = new();

            // Create styles
            doc.AddStyle(yellowIcon);
            doc.AddStyle(greenIcon);
            doc.AddStyle(redIcon);
            doc.AddStyle(towerIcon);

            foreach (Style s in plotStyle.Values)
            {
                doc.AddStyle(s);
            }
            

            doc.Description = new Description()
            {
                Text = $"<![CDATA[Total Subscribers:{Subscribers.Count()}<br/>Correct Lat/Long: {Subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0).Count()}]]>"
            };

            //TODO: add comments with counts to the tower folder, sector folder, etc.
            IEnumerable<Folder> siteFolders = Towers.Select(generateTower);

            // Create our Root folder and add all of our siteFolders to it.
            foreach (var F in siteFolders) { doc.AddFeature(F); }

            OutputKml.Feature = doc;
        }

        /// <summary>
        /// Basic output of the instances Kml to a Kmz as specified in configuration.
        /// </summary>
        public void Save(string band = "")
        {
            string FileName;
            if (String.IsNullOrWhiteSpace(settings.FileName))
                if (band == "")
                {
                    FileName = $"{DateTime.Now:yyyy-MM-dd} - Subscriber Map.kmz";
                } else
                {
                    FileName = $"{DateTime.Now:yyyy-MM-dd} - Subscriber Map ({band}).kmz";
                }
                else
                    FileName = settings.FileName;

            KmlFile kmlFile = KmlFile.Create(OutputKml, true);
            using FileStream fs = new(FileName, FileMode.Create);
            using KmzFile kmz = KmzFile.Create(kmlFile);
            kmz.Save(fs);
        }
    }
}