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

namespace cnMaestroReporting.Output.KML
{
    public class Manager
    {
        public Settings settings = new Settings();
        public Kml OutputKml { get; }

        public Manager(IConfigurationSection configSection)
        {
            // Bind our configuration
            configSection.Bind(settings);

            if (OutputKml == null)
                OutputKml = new Kml();
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

        public void GenerateKML(
            IEnumerable<SubscriberRadioInfo> data, 
            IEnumerable<KeyValuePair<string, CnLocation>> towers, 
            IEnumerable<KeyValuePair<string, string>> aps)
        {
            // Base document to hold styles and items
            Document doc = new Document();

            // Create styles
            Style yellowIcon, greenIcon, whiteIcon, redIcon, towerIcon;
            redIcon = CreateIconStyle(nameof(redIcon), settings.Icons["Bad"]);
            yellowIcon = CreateIconStyle(nameof(yellowIcon), settings.Icons["Poor"]);
            greenIcon = CreateIconStyle(nameof(greenIcon), settings.Icons["Good"]);
            whiteIcon = CreateIconStyle(nameof(whiteIcon), settings.Icons["Unknown"]);
            towerIcon = CreateIconStyle(nameof(towerIcon), settings.Icons["Tower"]);
                                                             
            doc.AddStyle(yellowIcon);
            doc.AddStyle(greenIcon);
            doc.AddStyle(redIcon);
            doc.AddStyle(towerIcon);

            //TODO: add comments with counts to the tower folder, sector folder, etc.

            IEnumerable<Folder> siteFolders = towers.OrderBy(tower => tower.Key)
                .Select(tower =>
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
                
                // Loop through all the APs for this towers AP
                foreach (var ap in aps.Where(ap => ap.Value == tower.Key).OrderBy(ap => ap.Key))
                {
                    // Create this sectors folder to hold all subscribers
                    var sectorFolder = new Folder() { Name = ap.Key };

                    // Fetch all subscribers for this specific sector.
                    var sectorSubscribers = data.Where(sm => sm.Latitude != 0 && sm.Longitude != 0 && sm.APName == ap.Key).OrderBy(sm => sm.Name);

                    // Loop through and create all the placemarks for these sector subscribers.
                    foreach (var sm in sectorSubscribers)
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

                        smPlacemark.Description = new Description()
                        {
                            Text = $"<![CDATA[http://{sm.IP}<br/>MAC: {sm.Esn}<br/><br/>SM Power Level: {sm.SmAPL}<br/>SM Expected Power Level: {sm.SmEPL}<br/><br/>AP Power Level: {sm.ApAPL}<br/>AP Expected Power Level: {sm.ApEPL}]]>"
                        };

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

                        sectorFolder.AddFeature(smPlacemark);
                    }

                    towerFolder.AddFeature(sectorFolder);
                }

                towerFolder.AddFeature(towerPlacemark);
                return towerFolder;
            });

            // Create our Root folder and add all of our siteFolders to it.
            foreach (var F in siteFolders)
            {
                doc.AddFeature(F);
            }
           
            OutputKml.Feature = doc;
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