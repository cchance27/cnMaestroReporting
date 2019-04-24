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

namespace cnMaestroReporting.Output.KML
{
    public class Manager
    {
        public Settings settings = new Settings();
        public Kml OutputKml { get; }
        private Style yellowIcon { get; }
        private Style greenIcon { get; }
        private Style whiteIcon { get; }
        private Style redIcon { get; }
        private Style towerIcon { get; }

        private IEnumerable<SubscriberRadioInfo> _subscribers { get; }
        private IEnumerable<KeyValuePair<string, CnLocation>> _towers { get; }
        private IEnumerable<AccessPointRadioInfo> _accessPoints { get; }

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

            _subscribers = subscribers.OrderBy(sm => sm.Name);
            _towers = towers.OrderBy(tower => tower.Key);
            _accessPoints = accesspoints.OrderBy(ap => ap.Name); // Name sorted dictionary.
        }

        private Style CreateIconStyle(string name, StyleConfig config)
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

        public void GenerateKML() {
            // Base document to hold styles and items
            Document doc = new Document();

            // Create styles
            doc.AddStyle(yellowIcon);
            doc.AddStyle(greenIcon);
            doc.AddStyle(redIcon);
            doc.AddStyle(towerIcon);

            doc.Description = new Description()
            {
                Text = $"<![CDATA[Total Subscribers:{_subscribers.Count()}<br/>Correct Lat/Long: {_subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0).Count()}]]>"
            };

            //TODO: add comments with counts to the tower folder, sector folder, etc.
            IEnumerable<Folder> siteFolders = _towers.Select(generateTower);

            // Create our Root folder and add all of our siteFolders to it.
            foreach (var F in siteFolders) { doc.AddFeature(F); }
           
            OutputKml.Feature = doc;
        }

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
                    Text = $"<![CDATA[Total Subscribers:{_subscribers.Where(sm => sm.Tower == tower.Key).Count()}<br/>Correct Lat/Long Subs: {_subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0 && sm.Tower == tower.Key).Count()}]]>"
                };

                // Loop through all the APs for this towers AP
                foreach (var ap in _accessPoints.Where(ap => ap.Tower == tower.Key))
                {
                    var sectorFolder = generateSector(ap);
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
        private Folder generateSector(AccessPointRadioInfo ap)
        {
            // Create this sectors folder to hold all subscribers
            var sectorFolder = new Folder() { Name = ap.Name };

            // Fetch all subscribers for this specific sector.
            var sectorSubscribers = _subscribers.Where(sm => sm.Latitude != 0 && sm.Longitude != 0 && sm.APName == ap.Name);

            sectorFolder.Description = CreateDescriptionFromObject(ap);

            // Loop through and create all the placemarks for these sector subscribers.
            foreach (var sm in sectorSubscribers)
            {
                var smPlacemark = generateSmPlacemark(sm);
                sectorFolder.AddFeature(smPlacemark);
            }

            return sectorFolder;
        }

        /// <summary>
        ///  Take a subscriberRadioInfo and return a nice SmPlacemark with all necesary subscriber information and location information.
        /// </summary>
        /// <param name="sm"></param>
        private Placemark generateSmPlacemark(SubscriberRadioInfo sm)
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
            if (sm.ApAPL <= settings.Icons["Bad"].SignalLevel)
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(redIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Bad"].Visibility;
            }
            else if (sm.ApAPL <= settings.Icons["Poor"].SignalLevel)
            {
                smPlacemark.StyleUrl = new Uri($"#" + nameof(yellowIcon), UriKind.Relative);
                smPlacemark.Visibility = settings.Icons["Poor"].Visibility;
            }
            else if (sm.ApAPL <= settings.Icons["Good"].SignalLevel)
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
        Description CreateDescriptionFromObject(Object inputObject)
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

            return new Description() { Text = $"<![CDATA[{content.ToString()}]]>" };
        }

        string FormatTrimAfter(string value, string trimString) => value.Contains(trimString) ? value.Substring(0, value.IndexOf(trimString)).Trim() : value;

        string FormatTimeSpan(System.TimeSpan timeSpan)
        {
            string FormatPart(int quantity, string name) => quantity > 0 ? $"{quantity}{name}{(quantity > 1 ? "s" : "")}" : null;
            return string.Join(", ", new[] { FormatPart(timeSpan.Days, "d"), FormatPart(timeSpan.Hours, "hr"), FormatPart(timeSpan.Minutes, "m") }.Where(x => x != null));
        }

        /// <summary>
        /// Basic output of the instances Kml to a Kmz as specified in configuration.
        /// </summary>
        public void Save()
        {
            string FileName;
            if (String.IsNullOrWhiteSpace(settings.FileName))
                FileName = $"{DateTime.Now.ToString("yyyy-MM-dd")} - Subscriber Map.kmz";
            else
                FileName = settings.FileName;

            KmlFile kmlFile = KmlFile.Create(OutputKml, true);
            using (FileStream fs = new FileStream(FileName, FileMode.Create))
            {
                using (KmzFile kmz = KmzFile.Create(kmlFile))
                {
                    kmz.Save(fs);
                }
            }
        }
    }
}